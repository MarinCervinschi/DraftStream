using DraftStream.Application;
using DraftStream.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace DraftStream.Application.Snippets;

[WorkflowHandler("snippets")]
public sealed class SnippetsWorkflowHandler : IWorkflowHandler
{
    private readonly ILogger<SnippetsWorkflowHandler> _logger;

    public SnippetsWorkflowHandler(ILogger<SnippetsWorkflowHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Snippets workflow received message from {SenderName}: {Text}",
            message.SenderName, message.Text);

        return Task.CompletedTask;
    }
}
