using System.Text.Json;
using System.Text.Json.Serialization;

namespace DraftStream.Infrastructure.OpenRouter.ApiModels;

internal sealed class ChatTool
{
    [JsonPropertyName("function")]
    public required ChatFunction Function { get; init; }
}

internal sealed class ChatFunction
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("parameters")]
    public required JsonElement Parameters { get; init; }
}

internal sealed class ChatToolCall
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("function")]
    public required ChatToolCallFunction Function { get; init; }
}

internal sealed class ChatToolCallFunction
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; }
}
