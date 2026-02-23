namespace DraftStream.Application.Messaging;

public interface IMessageSource
{
    string SourceType { get; }

    Task StartAsync(Func<IncomingMessage, Task> onMessageReceived, CancellationToken cancellationToken);
}
