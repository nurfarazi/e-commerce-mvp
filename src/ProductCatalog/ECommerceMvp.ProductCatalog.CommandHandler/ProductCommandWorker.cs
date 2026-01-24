using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.Shared.Infrastructure;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace ECommerceMvp.ProductCatalog.CommandHandler;

/// <summary>
/// Background worker that consumes commands from RabbitMQ and executes them.
/// </summary>
public class ProductCommandWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ILogger<ProductCommandWorker> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public ProductCommandWorker(
        IServiceProvider serviceProvider,
        RabbitMqOptions rabbitMqOptions,
        ILogger<ProductCommandWorker> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _rabbitMqOptions = rabbitMqOptions ?? throw new ArgumentNullException(nameof(rabbitMqOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProductCommandWorker starting...");

        try
        {
            await ConnectToRabbitMqAsync(stoppingToken).ConfigureAwait(false);
            await ConsumeCommandsAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProductCommandWorker stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProductCommandWorker encountered an error");
            throw;
        }
    }

    private async Task ConnectToRabbitMqAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqOptions.HostName,
            Port = _rabbitMqOptions.Port,
            UserName = _rabbitMqOptions.UserName,
            Password = _rabbitMqOptions.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: "productcatalog.commands",
            durable: true,
            exclusive: false,
            autoDelete: false);

        _channel.BasicQos(0, 1, false);

        _logger.LogInformation("Connected to RabbitMQ");
        
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task ConsumeCommandsAsync(CancellationToken cancellationToken)
    {
        if (_channel == null)
            throw new InvalidOperationException("Channel not initialized");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = System.Text.Encoding.UTF8.GetString(body);

                _logger.LogDebug("Received command: {Json}", json);

                // For now, just acknowledge (in real implementation, route to appropriate handler)
                _channel.BasicAck(ea.DeliveryTag, false);
                
                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(
            queue: "productcatalog.commands",
            autoAck: false,
            consumerTag: "product-command-consumer",
            consumer: consumer);

        _logger.LogInformation("Consuming commands from productcatalog.commands queue...");

        // Keep running
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
            _channel.Close();
        if (_connection != null)
            _connection.Close();

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}

