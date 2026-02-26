namespace DraftStream.Application.Workflows;

public sealed class WorkflowConfig
{
    public required string DatabaseId { get; init; }

    public string? ModelOverride { get; init; }
}
