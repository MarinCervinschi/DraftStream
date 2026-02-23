namespace DraftStream.Infrastructure.Telegram;

public sealed class TelegramSettings
{
    public const string SectionName = "Telegram";

    public required string BotToken { get; init; }

    public required long GroupId { get; init; }

    public required Dictionary<int, string> TopicMappings { get; init; }
}
