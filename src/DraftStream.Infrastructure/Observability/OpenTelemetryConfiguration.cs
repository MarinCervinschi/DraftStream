using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DraftStream.Infrastructure.Observability;

public static class OpenTelemetryConfiguration
{
    public static readonly ActivitySource ActivitySource = new("DraftStream");

    public static IServiceCollection AddDraftStreamOpenTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("DraftStream"))
            .WithTracing(tracing => tracing
                .AddSource(ActivitySource.Name)
                .AddConsoleExporter());

        return services;
    }
}
