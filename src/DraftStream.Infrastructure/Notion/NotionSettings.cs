namespace DraftStream.Infrastructure.Notion;

public sealed class NotionSettings
{
    public const string SectionName = "Notion";

    public required string IntegrationToken { get; init; }

    public Dictionary<string, string> DatabaseIds { get; init; } = new();
}
