namespace DraftStream.Application.Llm;

public sealed class LlmRequest
{
    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    public IReadOnlyList<LlmToolDefinition>? Tools { get; init; }

    public string? ModelOverride { get; init; }
}
