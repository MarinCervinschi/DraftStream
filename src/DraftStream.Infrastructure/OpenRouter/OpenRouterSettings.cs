namespace DraftStream.Infrastructure.OpenRouter;

public sealed class OpenRouterSettings
{
    public const string SectionName = "OpenRouter";

    public required string ApiKey { get; init; }

    public required string DefaultModel { get; init; }

    public Dictionary<string, string> ModelOverrides { get; init; } = new();
}
