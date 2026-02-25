using System.Net.Http.Headers;
using DraftStream.Application;
using DraftStream.Application.Llm;
using DraftStream.Application.Mcp;
using DraftStream.Application.Messaging;
using DraftStream.Application.Notes;
using DraftStream.Application.Snippets;
using DraftStream.Application.Tasks;
using DraftStream.Infrastructure.Messaging;
using DraftStream.Infrastructure.Notion;
using DraftStream.Infrastructure.Observability;
using DraftStream.Infrastructure.OpenRouter;
using DraftStream.Infrastructure.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace DraftStream.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDraftStreamOpenTelemetry();
        services.AddWorkflowHandlers();
        services.AddMessaging(configuration);
        services.AddOpenRouter(configuration);
        services.AddNotionMcp(configuration);
        return services;
    }

    private static void AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramSettings>(configuration.GetSection(TelegramSettings.SectionName));
        services.AddSingleton<IMessageSource, TelegramMessageSource>();
        services.AddSingleton<IMessageDispatcher, MessageDispatcher>();
        services.AddHostedService<MessageSourceBackgroundService>();
    }

    private static void AddWorkflowHandlers(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IWorkflowHandler, NotesWorkflowHandler>("notes");
        services.AddKeyedSingleton<IWorkflowHandler, TasksWorkflowHandler>("tasks");
        services.AddKeyedSingleton<IWorkflowHandler, SnippetsWorkflowHandler>("snippets");
    }

    private static void AddOpenRouter(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenRouterSettings>(configuration.GetSection(OpenRouterSettings.SectionName));

        services.AddHttpClient<ILlmClient, OpenRouterClient>((sp, client) =>
            {
                OpenRouterSettings settings = sp.GetRequiredService<IOptions<OpenRouterSettings>>().Value;

                client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", settings.ApiKey);
                client.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/draftstream");
                client.DefaultRequestHeaders.Add("X-Title", "DraftStream");
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
            });
    }

    private static void AddNotionMcp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotionSettings>(configuration.GetSection(NotionSettings.SectionName));
        services.AddSingleton<IMcpToolClient, NotionMcpClient>();
    }
}
