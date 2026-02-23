using DraftStream.Host;
using DraftStream.Infrastructure;
using DraftStream.Infrastructure.Configuration;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting DraftStream host");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Configuration.AddDraftStreamInfisical(builder.Configuration);

    builder.Services.AddSerilog(config => config.ReadFrom.Configuration(builder.Configuration));

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddHostedService<HealthCheckWorker>();

    IHost app = builder.Build();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DraftStream host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
