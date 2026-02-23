using DraftStream.Application;
using DraftStream.Application.Messaging;
using DraftStream.Application.Notes;
using DraftStream.Application.Snippets;
using DraftStream.Application.Tasks;
using DraftStream.Infrastructure.Messaging;
using DraftStream.Infrastructure.Observability;
using DraftStream.Infrastructure.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
}
