using System.Diagnostics;
using DraftStream.Application.Messaging;
using DraftStream.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DraftStream.Infrastructure.Telegram;

public sealed class TelegramMessageSource : IMessageSource
{
    public string SourceType => "telegram";

    private readonly TelegramSettings _settings;
    private readonly ILogger<TelegramMessageSource> _logger;

    public TelegramMessageSource(
        IOptions<TelegramSettings> settings,
        ILogger<TelegramMessageSource> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(
        Func<IncomingMessage, Task> onMessageReceived,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.BotToken))
        {
            _logger.LogWarning("Telegram BotToken is not configured, skipping Telegram message source");
            return;
        }

        var client = new TelegramBotClient(_settings.BotToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message],
            DropPendingUpdates = true
        };

        _logger.LogInformation("Telegram message source started for group {GroupId}", _settings.GroupId);

        await client.ReceiveAsync(
            updateHandler: (bot, update, ct) => HandleUpdateAsync(update, onMessageReceived, ct),
            errorHandler: (bot, exception, ct) => HandleErrorAsync(exception),
            receiverOptions: receiverOptions,
            cancellationToken: cancellationToken);
    }

    private async Task HandleUpdateAsync(
        Update update,
        Func<IncomingMessage, Task> onMessageReceived,
        CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: not null } message)
            return;

        if (message.Chat.Id != _settings.GroupId)
        {
            _logger.LogDebug(
                "Ignoring message from chat {ChatId}, expected group {GroupId}",
                message.Chat.Id, _settings.GroupId);
            return;
        }

        using Activity? activity = OpenTelemetryConfiguration.ActivitySource
            .StartActivity("TelegramMessageReceived");
        activity?.SetTag("telegram.chat_id", message.Chat.Id);
        activity?.SetTag("telegram.message_id", message.MessageId);
        activity?.SetTag("telegram.thread_id", message.MessageThreadId);

        string? workflowName = ResolveWorkflowName(message.MessageThreadId);

        if (workflowName is null)
        {
            _logger.LogWarning(
                "No workflow mapping for topic thread {ThreadId} in group {GroupId}",
                message.MessageThreadId, _settings.GroupId);
            return;
        }

        activity?.SetTag("workflow", workflowName);

        var incomingMessage = new IncomingMessage
        {
            WorkflowName = workflowName,
            Text = message.Text,
            SenderName = message.From?.FirstName ?? "Unknown",
            ReceivedAt = DateTimeOffset.UtcNow,
            SourceType = SourceType,
            SourceContext = new Dictionary<string, string>
            {
                ["ChatId"] = message.Chat.Id.ToString(),
                ["MessageId"] = message.MessageId.ToString(),
                ["ThreadId"] = message.MessageThreadId?.ToString() ?? ""
            }
        };

        _logger.LogInformation(
            "Received message from {SenderName} in workflow '{WorkflowName}' (thread {ThreadId})",
            incomingMessage.SenderName, workflowName, message.MessageThreadId);

        try
        {
            await onMessageReceived(incomingMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process Telegram message {MessageId} in workflow '{WorkflowName}'",
                message.MessageId, workflowName);
        }
    }

    private Task HandleErrorAsync(Exception exception)
    {
        _logger.LogError(exception, "Telegram polling error occurred");
        return Task.CompletedTask;
    }

    private string? ResolveWorkflowName(int? messageThreadId)
    {
        if (messageThreadId is null)
            return null;

        return _settings.TopicMappings.GetValueOrDefault(messageThreadId.Value);
    }
}
