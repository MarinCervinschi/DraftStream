using DraftStream.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace DraftStream.Application.Tasks;

[WorkflowHandler("tasks")]
public sealed class TasksWorkflowHandler : IWorkflowHandler
{
    private readonly ILogger<TasksWorkflowHandler> _logger;

    public TasksWorkflowHandler(ILogger<TasksWorkflowHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Tasks workflow received message from {SenderName}: {Text}",
            message.SenderName, message.Text);

        return Task.CompletedTask;
    }
}
