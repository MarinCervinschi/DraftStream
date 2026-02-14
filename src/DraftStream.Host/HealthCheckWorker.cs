namespace DraftStream.Host;

public sealed class HealthCheckWorker : BackgroundService
{
    private readonly ILogger<HealthCheckWorker> _logger;

    public HealthCheckWorker(ILogger<HealthCheckWorker> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DraftStream host is running");
        return Task.CompletedTask;
    }
}
