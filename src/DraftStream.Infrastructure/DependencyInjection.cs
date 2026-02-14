using System.Reflection;
using DraftStream.Application;
using DraftStream.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace DraftStream.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDraftStreamOpenTelemetry();
        services.AddWorkflowHandlers();
        return services;
    }

    private static void AddWorkflowHandlers(this IServiceCollection services)
    {
        IEnumerable<Type> handlerTypes = AppDomain.CurrentDomain.GetAssemblies()
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
