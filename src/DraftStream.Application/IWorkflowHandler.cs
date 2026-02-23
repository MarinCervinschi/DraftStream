using DraftStream.Application.Messaging;

namespace DraftStream.Application;

public interface IWorkflowHandler
{
    Task HandleAsync(IncomingMessage message, CancellationToken cancellationToken);
}
