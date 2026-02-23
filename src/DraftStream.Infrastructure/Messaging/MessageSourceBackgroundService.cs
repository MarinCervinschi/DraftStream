using DraftStream.Application.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DraftStream.Infrastructure.Messaging;

public sealed class MessageSourceBackgroundService : BackgroundService
{
    private readonly IEnumerable<IMessageSource> _sources;
    private readonly IMessageDispatcher _dispatcher;
    private readonly ILogger<MessageSourceBackgroundService> _logger;

    public MessageSourceBackgroundService(
        IEnumerable<IMessageSource> sources,
        IMessageDispatcher dispatcher,
        ILogger<MessageSourceBackgroundService> logger)
    {
        _sources = sources;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IEnumerable<Task> sourceTasks = _sources.Select(source =>
        {
            _logger.LogInformation("Starting message source: {SourceType}", source.SourceType);
            return source.StartAsync(
                message => _dispatcher.DispatchAsync(message, stoppingToken),
                stoppingToken);
        });

        await Task.WhenAll(sourceTasks);
    }
}
