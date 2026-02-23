using System.Diagnostics;
using DraftStream.Application;
using DraftStream.Application.Messaging;
using DraftStream.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DraftStream.Infrastructure.Messaging;

public sealed class MessageDispatcher : IMessageDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageDispatcher> _logger;

    public MessageDispatcher(IServiceProvider serviceProvider, ILogger<MessageDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        using Activity? activity = OpenTelemetryConfiguration.ActivitySource
            .StartActivity("DispatchMessage");
        activity?.SetTag("workflow", message.WorkflowName);
        activity?.SetTag("source", message.SourceType);

        IWorkflowHandler? handler = _serviceProvider
            .GetKeyedService<IWorkflowHandler>(message.WorkflowName);

        if (handler is null)
        {
            _logger.LogWarning(
                "No workflow handler registered for workflow '{WorkflowName}' from source '{SourceType}'",
                message.WorkflowName, message.SourceType);
            return;
        }

        _logger.LogInformation(
            "Dispatching message to workflow '{WorkflowName}' from {SenderName} via {SourceType}",
            message.WorkflowName, message.SenderName, message.SourceType);

        await handler.HandleAsync(message, cancellationToken);
    }
}
