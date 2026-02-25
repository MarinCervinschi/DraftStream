using System.Text.Json.Serialization;

namespace DraftStream.Infrastructure.OpenRouter.ApiModels;

internal sealed class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("choices")]
    public required List<ChatChoice> Choices { get; init; }

    [JsonPropertyName("usage")]
    public ChatUsage? Usage { get; init; }
}

internal sealed class ChatChoice
{
    [JsonPropertyName("message")]
    public required ChatMessage Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

internal sealed class ChatUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }
}
