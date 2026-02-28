using System.ClientModel;
using System.ClientModel.Primitives;
using DraftStream.Application;
using DraftStream.Application.Fallback;
using DraftStream.Application.Mcp;
using DraftStream.Application.Messaging;
using DraftStream.Application.Prompts;
using DraftStream.Application.Workflows;
using DraftStream.Infrastructure.Messaging;
using DraftStream.Infrastructure.Notion;
using DraftStream.Infrastructure.Observability;
using DraftStream.Infrastructure.OpenRouter;
using DraftStream.Infrastructure.Telegram;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using Polly;

namespace DraftStream.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddDraftStreamOpenTelemetry();
        services.AddFallbackStorage();
        services.AddWorkflowHandlers(configuration);
        services.AddMessaging(configuration);
        services.AddLlmClient(configuration);
        services.AddNotionMcp(configuration);
        return services;
    }

    private static void AddFallbackStorage(this IServiceCollection services) =>
        services.AddSingleton<IFallbackStorage, NotionFallbackStorage>();

    private static void AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramSettings>(configuration.GetSection(TelegramSettings.SectionName));
        services.AddSingleton<IMessageSource, TelegramMessageSource>();
        services.AddSingleton<IMessageDispatcher, MessageDispatcher>();
        services.AddHostedService<MessageSourceBackgroundService>();
    }

    private static void AddWorkflowHandlers(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkflowSettings>(configuration.GetSection(WorkflowSettings.SectionName));
        services.AddSingleton<PromptBuilder>();

        WorkflowSettings workflowSettings = configuration
            .GetSection(WorkflowSettings.SectionName)
            .Get<WorkflowSettings>() ?? new WorkflowSettings();

        if (workflowSettings.Items.Count == 0)
        {
            Console.WriteLine(
                "[WRN] No workflow configurations found under Workflows:Items. No handlers will be registered.");
        }

        foreach (KeyValuePair<string, WorkflowConfig> entry in workflowSettings.Items)
        {
            string workflowName = entry.Key;
            WorkflowConfig config = entry.Value;

            Console.WriteLine($"[INF] Registering workflow handler '{workflowName}'");

            services.AddKeyedScoped<IWorkflowHandler>(workflowName, (sp, _) =>
                new SchemaWorkflowHandler(
                    sp.GetRequiredService<IChatClient>(),
                    sp.GetRequiredService<IMcpToolProvider>(),
                    config,
                    sp.GetRequiredService<PromptBuilder>(),
                    sp.GetRequiredService<IFallbackStorage>(),
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<ILogger<SchemaWorkflowHandler>>()));
        }
    }

    private static void AddLlmClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenRouterSettings>(configuration.GetSection(OpenRouterSettings.SectionName));

        services.AddHttpClient("OpenRouter", client =>
            {
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
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
            });

        services.AddChatClient(sp =>
            {
                OpenRouterSettings settings = sp.GetRequiredService<IOptions<OpenRouterSettings>>().Value;
                IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                HttpClient httpClient = httpClientFactory.CreateClient("OpenRouter");

                var openAiClient = new OpenAIClient(
                    new ApiKeyCredential(settings.ApiKey),
                    new OpenAIClientOptions
                    {
                        Endpoint = new Uri("https://openrouter.ai/api/v1"),
                        Transport = new HttpClientPipelineTransport(httpClient)
                    });

                return openAiClient
                    .GetChatClient(settings.DefaultModel)
                    .AsIChatClient();
            })
            .UseFunctionInvocation();
    }

    private static void AddNotionMcp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotionSettings>(configuration.GetSection(NotionSettings.SectionName));
        services.AddSingleton<IMcpToolProvider, NotionMcpClient>();
    }
}
