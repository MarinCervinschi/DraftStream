namespace DraftStream.Application.Fallback;

public sealed class FallbackSettings
{
    public const string SectionName = "Fallback";

    public required string GeneralDatabaseId { get; init; }
}
