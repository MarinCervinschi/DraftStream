using DraftStream.Application.Fallback;
using DraftStream.Application.Mcp;
using DraftStream.Application.Messaging;
using DraftStream.Application.Prompts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DraftStream.Application.Workflows;

public sealed class SchemaWorkflowHandler : IWorkflowHandler
{
    private static readonly TimeSpan _toolCacheDuration = TimeSpan.FromMinutes(30);

    private static readonly HashSet<string> _cacheableToolNames = new(StringComparer.Ordinal)
    {
        "API-retrieve-a-database",
        "API-retrieve-a-data-source"
    };

    private readonly IChatClient _chatClient;
    private readonly IMcpToolProvider _mcpToolProvider;
    private readonly WorkflowConfig _config;
    private readonly PromptBuilder _promptBuilder;
    private readonly IFallbackStorage _fallbackStorage;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SchemaWorkflowHandler> _logger;

    public SchemaWorkflowHandler(
        IChatClient chatClient,
        IMcpToolProvider mcpToolProvider,
        WorkflowConfig config,
        PromptBuilder promptBuilder,
        IFallbackStorage fallbackStorage,
        IMemoryCache cache,
        ILogger<SchemaWorkflowHandler> logger)
    {
        _chatClient = chatClient;
        _mcpToolProvider = mcpToolProvider;
        _config = config;
        _promptBuilder = promptBuilder;
        _fallbackStorage = fallbackStorage;
        _cache = cache;
        _logger = logger;
    }

    public async Task HandleAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing '{WorkflowName}' workflow message from {SenderName}",
            message.WorkflowName, message.SenderName);

        try
        {
            IReadOnlyList<AITool> tools = await _mcpToolProvider.GetToolsAsync(cancellationToken);
            IList<AITool> wrappedTools = WrapCacheableTools(tools);

            string systemPrompt = _promptBuilder.BuildSystemPrompt(
                message.WorkflowName, _config.DatabaseId, message.SourceType);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, message.Text)
            };

            var options = new ChatOptions { Tools = [.. wrappedTools] };

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

    private IList<AITool> WrapCacheableTools(IReadOnlyList<AITool> tools)
    {
        var result = new List<AITool>(tools.Count);

        foreach (AITool tool in tools)
        {
            if (tool is AIFunction function && _cacheableToolNames.Contains(function.Name))
            {
                result.Add(new CachingAiFunction(function, _cache, _toolCacheDuration));
            }
            else
            {
                result.Add(tool);
            }
        }

        return result;
    }
}
