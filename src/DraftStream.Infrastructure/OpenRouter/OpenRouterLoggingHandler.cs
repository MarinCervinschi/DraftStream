using Microsoft.Extensions.Logging;

namespace DraftStream.Infrastructure.OpenRouter;

internal sealed class OpenRouterLoggingHandler(ILogger<OpenRouterLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError(
            "OpenRouter returned {StatusCode} for {Method} {Uri}: {ResponseBody}",
            (int)response.StatusCode, request.Method, request.RequestUri, body);

        return response;
    }
}
