namespace DraftStream.Application.Llm;

public sealed class LlmMessage
{
    public required string Role { get; init; }

    public required string Content { get; init; }

    public IReadOnlyList<LlmToolCall>? ToolCalls { get; init; }

    public string? ToolCallId { get; init; }
}
