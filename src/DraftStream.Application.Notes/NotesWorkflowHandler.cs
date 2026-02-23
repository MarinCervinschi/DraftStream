using DraftStream.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace DraftStream.Application.Notes;

[WorkflowHandler("notes")]
public sealed class NotesWorkflowHandler : IWorkflowHandler
{
    private readonly ILogger<NotesWorkflowHandler> _logger;

    public NotesWorkflowHandler(ILogger<NotesWorkflowHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Notes workflow received message from {SenderName}: {Text}",
            message.SenderName, message.Text);

        return Task.CompletedTask;
    }
}
