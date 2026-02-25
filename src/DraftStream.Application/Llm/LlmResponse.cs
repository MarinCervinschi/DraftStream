namespace DraftStream.Application.Llm;

public sealed class LlmResponse
{
    public required string? Content { get; init; }

    public required IReadOnlyList<LlmToolCall> ToolCalls { get; init; }

    public required string Model { get; init; }

    public required int PromptTokens { get; init; }

    public required int CompletionTokens { get; init; }
}
