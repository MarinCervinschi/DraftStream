namespace DraftStream.Application.Messaging;

public sealed class IncomingMessage
{
    public required string WorkflowName { get; init; }

    public required string Text { get; init; }

    public required string SenderName { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required string SourceType { get; init; }

    public Dictionary<string, string> SourceContext { get; init; } = new();
}
