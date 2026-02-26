using System.Collections.Concurrent;
using System.Text.Json;
using DraftStream.Application.Llm;
using DraftStream.Application.Mcp;
using DraftStream.Application.Messaging;
using DraftStream.Application.Prompts;
using Microsoft.Extensions.Logging;

namespace DraftStream.Application.Workflows;

public sealed class SchemaWorkflowHandler : IWorkflowHandler
{
    private const int _maxToolLoopIterations = 10;
    private const string _retrieveDatabaseToolName = "notion_retrieve_database";

    private static readonly ConcurrentDictionary<string, string> _schemaCache = new();

    private readonly ILlmClient _llmClient;
    private readonly IMcpToolClient _mcpToolClient;
    private readonly WorkflowConfig _config;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<SchemaWorkflowHandler> _logger;

    public SchemaWorkflowHandler(
        ILlmClient llmClient,
        IMcpToolClient mcpToolClient,
        WorkflowConfig config,
        PromptBuilder promptBuilder,
        ILogger<SchemaWorkflowHandler> logger)
    {
        _llmClient = llmClient;
        _mcpToolClient = mcpToolClient;
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

            string systemPrompt = _promptBuilder.BuildSystemPrompt(
                message.WorkflowName, _config.DatabaseId, schemaDescription);

            IReadOnlyList<LlmToolDefinition> tools =
                await _mcpToolClient.GetToolDefinitionsAsync(cancellationToken);

            var messages = new List<LlmMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = message.Text }
            };

            string? confirmationText = await RunToolLoopAsync(messages, tools, cancellationToken);

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

    private async Task<string?> RunToolLoopAsync(
        List<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < _maxToolLoopIterations; iteration++)
        {
            var request = new LlmRequest
            {
                Messages = messages,
                Tools = tools,
                ModelOverride = _config.ModelOverride
            };

            LlmResponse response = await _llmClient.CompleteAsync(request, cancellationToken);

            if (response.ToolCalls.Count == 0)
            {
                return response.Content;
            }

            messages.Add(new LlmMessage
            {
                Role = "assistant",
                Content = response.Content,
                ToolCalls = response.ToolCalls
            });

            foreach (LlmToolCall toolCall in response.ToolCalls)
            {
                _logger.LogInformation(
                    "Executing MCP tool '{ToolName}' (iteration {Iteration})",
                    toolCall.FunctionName, iteration + 1);

                McpToolResult result = await _mcpToolClient.CallToolAsync(
                    toolCall.FunctionName, toolCall.ArgumentsJson, cancellationToken);

                if (result.IsError)
                {
                    _logger.LogWarning(
                        "MCP tool '{ToolName}' returned an error: {ErrorContent}",
                        toolCall.FunctionName, result.Content);
                }

                messages.Add(new LlmMessage
                {
                    Role = "tool",
                    Content = result.Content,
                    ToolCallId = toolCall.Id
                });
            }
        }

        _logger.LogWarning(
            "Tool loop reached maximum iterations ({MaxIterations}) without a final response",
            _maxToolLoopIterations);

        return "Your message was processed, but the response may be incomplete.";
    }

    private async Task<string> FetchSchemaDescriptionAsync(CancellationToken cancellationToken)
    {
        if (_schemaCache.TryGetValue(_config.DatabaseId, out string? cached))
            return cached;

        _logger.LogInformation(
            "Fetching database schema for {DatabaseId}", _config.DatabaseId);

        string argumentsJson = JsonSerializer.Serialize(new { database_id = _config.DatabaseId });

        McpToolResult result = await _mcpToolClient.CallToolAsync(
            _retrieveDatabaseToolName, argumentsJson, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning(
                "Failed to fetch schema for database {DatabaseId}: {Error}",
                _config.DatabaseId, result.Content);
            return "Schema not available. Use the database ID directly with the tools.";
        }

        string schemaDescription = PromptBuilder.FormatSchemaDescription(result.Content);
        _schemaCache.TryAdd(_config.DatabaseId, schemaDescription);

        _logger.LogInformation(
            "Cached database schema for {DatabaseId}", _config.DatabaseId);

        return schemaDescription;
    }
}
