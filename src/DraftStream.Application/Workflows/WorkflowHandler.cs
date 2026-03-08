using DraftStream.Application.Fallback;
using DraftStream.Application.Mcp;
using DraftStream.Application.Messaging;
using DraftStream.Application.Prompts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DraftStream.Application.Workflows;

public sealed class WorkflowHandler : IWorkflowHandler
{
    private readonly IChatClient _chatClient;
    private readonly IMcpToolProvider _mcpToolProvider;
    private readonly ISchemaProvider _schemaProvider;
    private readonly WorkflowConfig _config;
    private readonly PromptBuilder _promptBuilder;
    private readonly IFallbackStorage _fallbackStorage;
    private readonly ILogger<WorkflowHandler> _logger;

    public WorkflowHandler(
        IChatClient chatClient,
        IMcpToolProvider mcpToolProvider,
        ISchemaProvider schemaProvider,
        WorkflowConfig config,
        PromptBuilder promptBuilder,
        IFallbackStorage fallbackStorage,
        ILogger<WorkflowHandler> logger)
    {
        _chatClient = chatClient;
        _mcpToolProvider = mcpToolProvider;
        _schemaProvider = schemaProvider;
        _config = config;
        _promptBuilder = promptBuilder;
        _fallbackStorage = fallbackStorage;
        _logger = logger;
    }

    public async Task HandleAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing '{WorkflowName}' workflow message from {SenderName}",
            message.WorkflowName, message.SenderName);

        try
        {
            DatabaseSchema schema = await _schemaProvider.GetSchemaAsync(
                _config.DatabaseId, cancellationToken);

            string systemPrompt = _promptBuilder.BuildSystemPrompt(
                message.WorkflowName, message.SourceType, schema);

            IReadOnlyList<AITool> allTools = await _mcpToolProvider.GetToolsAsync(cancellationToken);
            IList<AITool> tools = FilterToPostPageOnly(allTools);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, message.Text)
            };

            var options = new ChatOptions { Tools = [.. tools] };

            if (!string.IsNullOrEmpty(_config.ModelOverride))
            {
                options.ModelId = _config.ModelOverride;
            }

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

            string replyText = await AttemptFallbackSaveAsync(message, cancellationToken);

            if (message.ReplyAsync is not null)
            {
                try
                {
                    await message.ReplyAsync(replyText, cancellationToken);
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

    private static IList<AITool> FilterToPostPageOnly(IReadOnlyList<AITool> tools)
    {
        return tools
            .Where(t => t is AIFunction { Name: "API-post-page" })
            .ToList();
    }

    private async Task<string> AttemptFallbackSaveAsync(
        IncomingMessage message, CancellationToken cancellationToken)
    {
        bool saved = await _fallbackStorage.SaveToWorkflowDatabaseAsync(
            _config.DatabaseId,
            message.Text,
            message.SenderName,
            message.SourceType,
            message.WorkflowName,
            cancellationToken);

        return saved
            ? "Processing failed, but your message was saved directly to the database."
            : "Sorry, I couldn't process your message and saving it failed too. Please try again.";
    }
}
