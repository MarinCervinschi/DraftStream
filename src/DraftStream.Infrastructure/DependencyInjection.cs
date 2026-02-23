using System.Reflection;
using DraftStream.Application;
using DraftStream.Application.Messaging;
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

    private static readonly Type[] _handlerAssemblyAnchors =
    [
        typeof(Application.Notes.NotesWorkflowHandler),
        typeof(Application.Tasks.TasksWorkflowHandler),
        typeof(Application.Snippets.SnippetsWorkflowHandler),
    ];

    private static void AddWorkflowHandlers(this IServiceCollection services)
    {
        IEnumerable<Type> handlerTypes = _handlerAssemblyAnchors
            .Select(t => t.Assembly)
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(IWorkflowHandler).IsAssignableFrom(t)
                        && t.GetCustomAttribute<WorkflowHandlerAttribute>() is not null);

        foreach (Type type in handlerTypes)
        {
            string name = type.GetCustomAttribute<WorkflowHandlerAttribute>()!.Name;
            services.AddKeyedSingleton(typeof(IWorkflowHandler), name, type);
        }
    }
}
