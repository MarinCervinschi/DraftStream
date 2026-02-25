namespace DraftStream.Application.Llm;

public sealed class LlmToolDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string ParametersSchemaJson { get; init; }
}
