using DraftStream.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace DraftStream.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDraftStreamOpenTelemetry();
        return services;
    }
}
