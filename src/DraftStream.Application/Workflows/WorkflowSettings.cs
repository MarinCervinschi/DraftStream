namespace DraftStream.Application.Workflows;

public sealed class WorkflowSettings
{
    public const string SectionName = "Workflows";

    public Dictionary<string, WorkflowConfig> Items { get; init; } = new();
}
