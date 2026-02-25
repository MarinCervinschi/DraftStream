namespace DraftStream.Application.Llm;

public sealed class LlmToolCall
{
    public required string Id { get; init; }

    public required string FunctionName { get; init; }

    public required string ArgumentsJson { get; init; }
}
