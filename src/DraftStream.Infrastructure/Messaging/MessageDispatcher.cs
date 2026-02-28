using System.Diagnostics;
using DraftStream.Application;
using DraftStream.Application.Fallback;
using DraftStream.Application.Messaging;
using DraftStream.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DraftStream.Infrastructure.Messaging;

public sealed class MessageDispatcher : IMessageDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IFallbackStorage _fallbackStorage;
    private readonly ILogger<MessageDispatcher> _logger;

    public MessageDispatcher(
        IServiceProvider serviceProvider,
        IFallbackStorage fallbackStorage,
        ILogger<MessageDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _fallbackStorage = fallbackStorage;
        _logger = logger;
    }

    public async Task DispatchAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        using Activity? activity = OpenTelemetryConfiguration.ActivitySource
            .StartActivity("DispatchMessage");
        activity?.SetTag("workflow", message.WorkflowName);
        activity?.SetTag("source", message.SourceType);

        using IServiceScope scope = _serviceProvider.CreateScope();

        IWorkflowHandler? handler = scope.ServiceProvider
            .GetKeyedService<IWorkflowHandler>(message.WorkflowName);

        if (handler is null)
        {
            _logger.LogWarning(
                "No workflow handler registered for workflow '{WorkflowName}' from source '{SourceType}', attempting fallback save",
                message.WorkflowName, message.SourceType);

            await SaveUnhandledMessageAsync(message, cancellationToken);
            return;
        }

        _logger.LogInformation(
            "Dispatching message to workflow '{WorkflowName}' from {SenderName} via {SourceType}",
            message.WorkflowName, message.SenderName, message.SourceType);

        await handler.HandleAsync(message, cancellationToken);
    }

    private async Task SaveUnhandledMessageAsync(
        IncomingMessage message, CancellationToken cancellationToken)
    {
        string sourceContext = $"workflow:{message.WorkflowName}, source:{message.SourceType}";

        bool saved = await _fallbackStorage.SaveToGeneralFallbackAsync(
            message.Text, message.SenderName, sourceContext, cancellationToken);

        if (message.ReplyAsync is null)
            return;

        try
        {
            string replyText = saved
                ? "This topic isn't configured for a workflow, but your message was saved to the fallback inbox."
                : "This topic isn't configured for a workflow, and saving to fallback failed. Please try again.";

            await message.ReplyAsync(replyText, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send fallback reply for unhandled workflow '{WorkflowName}'",
                message.WorkflowName);
        }
    }
}
