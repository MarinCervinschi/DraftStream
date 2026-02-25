using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using DraftStream.Application.Llm;
using DraftStream.Infrastructure.Observability;
using DraftStream.Infrastructure.OpenRouter.ApiModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DraftStream.Infrastructure.OpenRouter;

public sealed class OpenRouterClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterSettings _settings;
    private readonly ILogger<OpenRouterClient> _logger;

    public OpenRouterClient(
        HttpClient httpClient,
        IOptions<OpenRouterSettings> settings,
        ILogger<OpenRouterClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        string model = request.ModelOverride ?? _settings.DefaultModel;

        using Activity? activity = OpenTelemetryConfiguration.ActivitySource
            .StartActivity("LlmComplete");
        activity?.SetTag("llm.model", model);
        activity?.SetTag("llm.message_count", request.Messages.Count);
        activity?.SetTag("llm.has_tools", request.Tools is { Count: > 0 });

        ChatCompletionRequest apiRequest = BuildApiRequest(request, model);

        _logger.LogInformation(
            "Sending chat completion request to OpenRouter with model '{Model}' ({MessageCount} messages)",
            model, request.Messages.Count);

        try
        {
            using HttpResponseMessage httpResponse = await _httpClient.PostAsJsonAsync(
                "chat/completions",
                apiRequest,
                cancellationToken);

            httpResponse.EnsureSuccessStatusCode();

            ChatCompletionResponse? apiResponse = await httpResponse.Content
                .ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken);

            if (apiResponse is null)
            {
                throw new InvalidOperationException(
                    $"OpenRouter returned null response body for model '{model}'");
            }

            LlmResponse response = MapToLlmResponse(apiResponse, model);

            activity?.SetTag("llm.prompt_tokens", response.PromptTokens);
            activity?.SetTag("llm.completion_tokens", response.CompletionTokens);
            activity?.SetTag("llm.tool_call_count", response.ToolCalls.Count);

            _logger.LogInformation(
                "OpenRouter response received for model '{Model}': {PromptTokens} prompt tokens, " +
                "{CompletionTokens} completion tokens, {ToolCallCount} tool calls",
                response.Model, response.PromptTokens, response.CompletionTokens, response.ToolCalls.Count);

            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP request to OpenRouter failed for model '{Model}'", model);
            throw new InvalidOperationException(
                $"Failed to complete chat request with OpenRouter for model '{model}'", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to deserialize OpenRouter response for model '{Model}'", model);
            throw new InvalidOperationException(
                $"Failed to parse OpenRouter response for model '{model}'", ex);
        }
    }

    private static ChatCompletionRequest BuildApiRequest(LlmRequest request, string model)
    {
        var messages = request.Messages.Select(m => new ChatMessage
        {
            Role = m.Role,
            Content = m.Content,
            ToolCalls = m.ToolCalls?.Select(tc => new ChatToolCall
            {
                Id = tc.Id,
                Function = new ChatToolCallFunction
                {
                    Name = tc.FunctionName,
                    Arguments = tc.ArgumentsJson
                }
            }).ToList(),
            ToolCallId = m.ToolCallId
        }).ToList();

        var tools = request.Tools?.Select(t => new ChatTool
        {
            Function = new ChatFunction
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = JsonDocument.Parse(t.ParametersSchemaJson).RootElement.Clone()
            }
        }).ToList();

        return new ChatCompletionRequest
        {
            Model = model,
            Messages = messages,
            Tools = tools is { Count: > 0 } ? tools : null
        };
    }

    private static LlmResponse MapToLlmResponse(ChatCompletionResponse apiResponse, string requestedModel)
    {
        ChatChoice? firstChoice = apiResponse.Choices.FirstOrDefault();

        string? content = firstChoice?.Message.Content;
        List<LlmToolCall> toolCalls = firstChoice?.Message.ToolCalls?
            .Select(tc => new LlmToolCall
            {
                Id = tc.Id,
                FunctionName = tc.Function.Name,
                ArgumentsJson = tc.Function.Arguments
            }).ToList() ?? [];

        return new LlmResponse
        {
            Content = content,
            ToolCalls = toolCalls,
            Model = apiResponse.Model ?? requestedModel,
            PromptTokens = apiResponse.Usage?.PromptTokens ?? 0,
            CompletionTokens = apiResponse.Usage?.CompletionTokens ?? 0
        };
    }
}
