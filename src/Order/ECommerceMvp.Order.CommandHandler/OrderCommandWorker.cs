namespace ECommerceMvp.Order.CommandHandler;

public class OrderCommandWorker : BackgroundService
{
    private readonly ILogger<OrderCommandWorker> _logger;

    public OrderCommandWorker(ILogger<OrderCommandWorker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order Command Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Order Command Worker is working at {Time}", DateTimeOffset.Now);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Order Command Worker is stopping.");
    }
}
