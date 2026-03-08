using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DraftStream.Infrastructure.Observability;

public static class OpenTelemetryConfiguration
{
    public static readonly ActivitySource ActivitySource = new("DraftStream");

    public static IServiceCollection AddDraftStreamOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string seqUrl = configuration["Seq:ServerUrl"] ?? "http://localhost:5341";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("DraftStream"))
            .WithTracing(tracing => tracing
                .AddSource(ActivitySource.Name)
                .AddConsoleExporter()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri($"{seqUrl}/ingest/otlp/v1/traces");
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                }));

        return services;
    }
}
