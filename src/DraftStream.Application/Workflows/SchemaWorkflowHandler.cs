using System.Collections.Concurrent;
using System.Text.Json;
using DraftStream.Application.Mcp;
using DraftStream.Application.Messaging;
using DraftStream.Application.Prompts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DraftStream.Application.Workflows;

public sealed class SchemaWorkflowHandler : IWorkflowHandler
{
    private const string _retrieveDatabaseToolName = "API-retrieve-a-database";
    private const string _retrieveDataSourceToolName = "API-retrieve-a-data-source";

    private static readonly ConcurrentDictionary<string, string> _schemaCache = new();

    private readonly IChatClient _chatClient;
    private readonly IMcpToolProvider _mcpToolProvider;
    private readonly WorkflowConfig _config;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<SchemaWorkflowHandler> _logger;

    public SchemaWorkflowHandler(
        IChatClient chatClient,
        IMcpToolProvider mcpToolProvider,
        WorkflowConfig config,
        PromptBuilder promptBuilder,
        ILogger<SchemaWorkflowHandler> logger)
    {
        _chatClient = chatClient;
        _mcpToolProvider = mcpToolProvider;
        _config = config;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task HandleAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing '{WorkflowName}' workflow message from {SenderName}",
            message.WorkflowName, message.SenderName);

        try
        {
            string schemaDescription = await FetchSchemaDescriptionAsync(cancellationToken);

            IReadOnlyList<AITool> tools = await _mcpToolProvider.GetToolsAsync(cancellationToken);

            string systemPrompt = _promptBuilder.BuildSystemPrompt(
                message.WorkflowName, _config.DatabaseId, schemaDescription);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, message.Text)
            };

            var options = new ChatOptions
            {
                Tools = [.. tools],
                ModelId = _config.ModelOverride
            };

            ChatResponse response = await _chatClient.GetResponseAsync(messages, options, cancellationToken);

            string confirmationText = response.Text;

            if (message.ReplyAsync is not null && !string.IsNullOrWhiteSpace(confirmationText))
            {
                await message.ReplyAsync(confirmationText, cancellationToken);
            }

            _logger.LogInformation(
                "Successfully processed '{WorkflowName}' workflow message from {SenderName}",
                message.WorkflowName, message.SenderName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to process '{WorkflowName}' workflow message from {SenderName}: {MessageText}",
                message.WorkflowName, message.SenderName, message.Text);

            if (message.ReplyAsync is not null)
            {
                try
                {
                    await message.ReplyAsync(
                        "Sorry, I couldn't process your message. Please try again.",
                        cancellationToken);
                }
                catch (Exception replyEx)
                {
                    _logger.LogWarning(replyEx,
                        "Failed to send error reply for '{WorkflowName}' workflow",
                        message.WorkflowName);
                }
            }
        }
    }

    private async Task<string> FetchSchemaDescriptionAsync(CancellationToken cancellationToken)
    {
        if (_schemaCache.TryGetValue(_config.DatabaseId, out string? cached))
            return cached;

        _logger.LogInformation(
            "Fetching database schema for {DatabaseId}", _config.DatabaseId);

        string dataSourceId = await FetchDataSourceIdAsync(cancellationToken);

        if (string.IsNullOrEmpty(dataSourceId))
            return "Schema not available. Use the database ID directly with the tools.";

        string dataSourceArgs = JsonSerializer.Serialize(new { data_source_id = dataSourceId });

        McpToolResult dataSourceResult = await _mcpToolProvider.CallToolDirectAsync(
            _retrieveDataSourceToolName, dataSourceArgs, cancellationToken);

        if (dataSourceResult.IsError)
        {
            _logger.LogWarning(
                "Failed to fetch data source {DataSourceId} for database {DatabaseId}: {Error}",
                dataSourceId, _config.DatabaseId, dataSourceResult.Content);
            return "Schema not available. Use the database ID directly with the tools.";
        }

        string schemaDescription = PromptBuilder.FormatSchemaDescription(dataSourceResult.Content);
        _schemaCache.TryAdd(_config.DatabaseId, schemaDescription);

        _logger.LogInformation(
            "Cached database schema for {DatabaseId} (data source {DataSourceId})",
            _config.DatabaseId, dataSourceId);

        return schemaDescription;
    }

    private async Task<string> FetchDataSourceIdAsync(CancellationToken cancellationToken)
    {
        string argumentsJson = JsonSerializer.Serialize(new { database_id = _config.DatabaseId });

        McpToolResult result = await _mcpToolProvider.CallToolDirectAsync(
            _retrieveDatabaseToolName, argumentsJson, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning(
                "Failed to retrieve database {DatabaseId}: {Error}",
                _config.DatabaseId, result.Content);
            return string.Empty;
        }

        using var doc = JsonDocument.Parse(result.Content);

        if (doc.RootElement.TryGetProperty("data_sources", out JsonElement dataSources)
            && dataSources.GetArrayLength() > 0)
        {
            string? id = dataSources[0].GetProperty("id").GetString();

            if (!string.IsNullOrEmpty(id))
            {
                _logger.LogInformation(
                    "Resolved data source {DataSourceId} for database {DatabaseId}",
                    id, _config.DatabaseId);
                return id;
            }
        }

        _logger.LogWarning(
            "No data sources found for database {DatabaseId}", _config.DatabaseId);
        return string.Empty;
    }
}
