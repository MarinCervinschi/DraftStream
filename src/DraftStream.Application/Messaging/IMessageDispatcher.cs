namespace DraftStream.Application.Messaging;

public interface IMessageDispatcher
{
    Task DispatchAsync(IncomingMessage message, CancellationToken cancellationToken);
}
