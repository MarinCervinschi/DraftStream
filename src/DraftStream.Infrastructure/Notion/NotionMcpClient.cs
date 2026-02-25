using System.Diagnostics;
using System.Text.Json;
using DraftStream.Application.Llm;
using DraftStream.Application.Mcp;
using DraftStream.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DraftStream.Infrastructure.Notion;

public sealed class NotionMcpClient : IMcpToolClient, IAsyncDisposable
{
    private readonly NotionSettings _settings;
    private readonly ILogger<NotionMcpClient> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private McpClient? _client;
    private IReadOnlyList<LlmToolDefinition>? _cachedToolDefinitions;
    private bool _disposed;

    public NotionMcpClient(
        IOptions<NotionSettings> settings,
        ILogger<NotionMcpClient> logger,
        ILoggerFactory loggerFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<IReadOnlyList<LlmToolDefinition>> GetToolDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        if (_cachedToolDefinitions is not null)
            return _cachedToolDefinitions;

        using Activity? activity = OpenTelemetryConfiguration.ActivitySource
            .StartActivity("McpListTools");
        activity?.SetTag("mcp.server", "notion");

        McpClient client = await EnsureConnectedAsync(cancellationToken);

        try
        {
            IList<McpClientTool> tools = await client.ListToolsAsync(
                cancellationToken: cancellationToken);

            _cachedToolDefinitions = tools.Select(MapToToolDefinition).ToList();

            activity?.SetTag("mcp.tool_count", _cachedToolDefinitions.Count);

            _logger.LogInformation(
                "Retrieved {ToolCount} tool definitions from Notion MCP server",
                _cachedToolDefinitions.Count);

            return _cachedToolDefinitions;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to retrieve tool definitions from Notion MCP server");
            await ResetConnectionAsync();
            throw new InvalidOperationException(
                "Failed to retrieve tool definitions from Notion MCP server", ex);
        }
    }

    public async Task<McpToolResult> CallToolAsync(
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        using Activity? activity = OpenTelemetryConfiguration.ActivitySource
            .StartActivity("McpCallTool");
        activity?.SetTag("mcp.server", "notion");
        activity?.SetTag("mcp.tool_name", toolName);

        McpClient client = await EnsureConnectedAsync(cancellationToken);

        Dictionary<string, object?>? arguments =
            JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);

        try
        {
            return await InvokeToolAsync(client, toolName, arguments, activity, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "MCP tool call '{ToolName}' failed, attempting reconnection",
                toolName);

            await ResetConnectionAsync();

            try
            {
                client = await EnsureConnectedAsync(cancellationToken);
                return await InvokeToolAsync(client, toolName, arguments, activity, cancellationToken);
            }
            catch (Exception retryEx) when (retryEx is not OperationCanceledException)
            {
                _logger.LogError(retryEx,
                    "MCP tool call '{ToolName}' failed after reconnection attempt",
                    toolName);
                throw new InvalidOperationException(
                    $"Failed to invoke MCP tool '{toolName}' on Notion MCP server after reconnection attempt",
                    retryEx);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_client is not null)
        {
            try
            {
                await _client.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Notion MCP client");
            }

            _client = null;
        }

        _connectionLock.Dispose();
    }

    private async Task<McpClient> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_client is not null)
            return _client;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_client is not null)
                return _client;

            _client = await CreateConnectionAsync(cancellationToken);
            return _client;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<McpClient> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.IntegrationToken))
            throw new InvalidOperationException(
                "Notion IntegrationToken is not configured. Set 'Notion:IntegrationToken' in configuration or Infisical.");

        using Activity? activity = OpenTelemetryConfiguration.ActivitySource
            .StartActivity("McpConnect");
        activity?.SetTag("mcp.server", "notion");
        activity?.SetTag("mcp.command", "npx @notionhq/notion-mcp-server");

        _logger.LogInformation("Connecting to Notion MCP server...");

        try
        {
            var transport = new StdioClientTransport(
                new StdioClientTransportOptions
                {
                    Command = "npx",
                    Arguments = ["-y", "@notionhq/notion-mcp-server"],
                    Name = "NotionMcp",
                    EnvironmentVariables = new Dictionary<string, string?>
                    {
                        ["NOTION_TOKEN"] = _settings.IntegrationToken
                    }
                },
                _loggerFactory);

            var options = new McpClientOptions
            {
                ClientInfo = new Implementation
                {
                    Name = "DraftStream",
                    Version = "1.0.0"
                }
            };

            McpClient client = await McpClient.CreateAsync(
                transport, options, _loggerFactory, cancellationToken);

            _logger.LogInformation(
                "Connected to Notion MCP server (server: {ServerName} {ServerVersion})",
                client.ServerInfo?.Name ?? "unknown",
                client.ServerInfo?.Version ?? "unknown");

            return client;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to establish connection to Notion MCP server via 'npx @notionhq/notion-mcp-server'");
            throw new InvalidOperationException(
                "Failed to establish connection to Notion MCP server via 'npx @notionhq/notion-mcp-server'", ex);
        }
    }

    private static async Task<McpToolResult> InvokeToolAsync(
        McpClient client,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        CallToolResult result = await client.CallToolAsync(
            toolName, arguments, cancellationToken: cancellationToken);

        bool isError = result.IsError ?? false;
        activity?.SetTag("mcp.is_error", isError);

        string content = string.Join("\n", result.Content
            .OfType<TextContentBlock>()
            .Select(c => c.Text));

        return new McpToolResult
        {
            Content = content,
            IsError = isError
        };
    }

    private async Task ResetConnectionAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_client is not null)
            {
                try
                {
                    await _client.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing Notion MCP client during reconnection");
                }

                _client = null;
            }

            _cachedToolDefinitions = null;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private static LlmToolDefinition MapToToolDefinition(McpClientTool tool)
    {
        return new LlmToolDefinition
        {
            Name = tool.Name,
            Description = tool.Description ?? string.Empty,
            ParametersSchemaJson = tool.JsonSchema.ToString()
        };
    }
}
