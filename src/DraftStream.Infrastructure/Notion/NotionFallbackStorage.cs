using System.Text.Json;
using DraftStream.Application.Fallback;
using DraftStream.Application.Mcp;
using Microsoft.Extensions.Logging;

namespace DraftStream.Infrastructure.Notion;

public sealed class NotionFallbackStorage : IFallbackStorage
{
    private const string _createPageToolName = "API-post-page";
    private const int _maxTitleLength = 100;

    private readonly IMcpToolProvider _mcpToolProvider;
    private readonly ILogger<NotionFallbackStorage> _logger;

    public NotionFallbackStorage(
        IMcpToolProvider mcpToolProvider,
        ILogger<NotionFallbackStorage> logger)
    {
        _mcpToolProvider = mcpToolProvider;
        _logger = logger;
    }

    public async Task<bool> SaveToWorkflowDatabaseAsync(
        string databaseId,
        string title,
        string messageText,
        string senderName,
        string sourceType,
        string workflowName,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Attempting fallback save to workflow '{WorkflowName}' database {DatabaseId} for message from {SenderName}",
            workflowName, databaseId, senderName);

        try
        {
            string truncatedTitle = TruncateTitle(title);
            string bodyContent = FormatPageBody(messageText, senderName, workflowName);

            string argumentsJson = BuildCreatePageArguments(databaseId, truncatedTitle, sourceType, bodyContent);

            McpToolResult result = await _mcpToolProvider.CallToolDirectAsync(
                _createPageToolName, argumentsJson, cancellationToken);

            if (result.IsError)
            {
                _logger.LogWarning(
                    "Fallback save returned error for workflow '{WorkflowName}' database {DatabaseId}: {Error}",
                    workflowName, databaseId, result.Content);
                return false;
            }

            _logger.LogInformation(
                "Fallback save succeeded for workflow '{WorkflowName}' database {DatabaseId}",
                workflowName, databaseId);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Fallback save failed for workflow '{WorkflowName}' database {DatabaseId}",
                workflowName, databaseId);
            return false;
        }
    }

    private static string TruncateTitle(string text)
    {
        string singleLine = text.ReplaceLineEndings(" ");
        return singleLine.Length <= _maxTitleLength
            ? singleLine
            : string.Concat(singleLine.AsSpan(0, _maxTitleLength - 3), "...");
    }

    private static string FormatPageBody(string messageText, string senderName, string context) =>
        $"From: {senderName}\nContext: {context}\nReceived: {DateTimeOffset.UtcNow:O}\n\n{messageText}";

    private static string BuildCreatePageArguments(
        string databaseId, string title, string sourceType, string bodyContent)
    {
        var arguments = new
        {
            parent = new { database_id = databaseId },
            properties = new
            {
                Title = new
                {
                    title = new[]
                    {
                        new { text = new { content = title } }
                    }
                },
                Source = new
                {
                    select = new { name = sourceType }
                }
            },
            children = new[]
            {
                new
                {
                    @object = "block",
                    type = "paragraph",
                    paragraph = new
                    {
                        rich_text = new[]
                        {
                            new { type = "text", text = new { content = bodyContent } }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(arguments);
    }
}
