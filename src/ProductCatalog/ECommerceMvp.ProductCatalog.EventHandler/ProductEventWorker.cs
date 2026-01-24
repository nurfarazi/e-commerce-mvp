using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.ProductCatalog.Domain;
using ECommerceMvp.ProductCatalog.Infrastructure;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Infrastructure;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace ECommerceMvp.ProductCatalog.EventHandler;

/// <summary>
/// Background worker that consumes events from RabbitMQ and projects them to read models.
/// </summary>
public class ProductEventWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ILogger<ProductEventWorker> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public ProductEventWorker(
        IServiceProvider serviceProvider,
        RabbitMqOptions rabbitMqOptions,
        ILogger<ProductEventWorker> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _rabbitMqOptions = rabbitMqOptions ?? throw new ArgumentNullException(nameof(rabbitMqOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProductEventWorker starting...");

        try
        {
            await ConnectToRabbitMqAsync(stoppingToken).ConfigureAwait(false);
            await ConsumeEventsAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProductEventWorker stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProductEventWorker encountered an error");
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
            DispatchConsumersAsync = false,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare fanout exchange
        _channel.ExchangeDeclare(
            exchange: "ProductCatalog.events",
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false);

        // Declare and bind queue
        _channel.QueueDeclare(
            queue: "productcatalog.projections",
            durable: true,
            exclusive: false,
            autoDelete: false);

        _channel.QueueBind(
            queue: "productcatalog.projections",
            exchange: "ProductCatalog.events",
            routingKey: string.Empty);

        _channel.BasicQos(0, 1, false);

        _logger.LogInformation("Connected to RabbitMQ");
    }

    private async Task ConsumeEventsAsync(CancellationToken cancellationToken)
    {
        if (_channel == null)
            throw new InvalidOperationException("Channel not initialized");

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = System.Text.Encoding.UTF8.GetString(body);

                _logger.LogDebug("Received event: {Json}", json);

                // Deserialize the event
                using var jsonDoc = JsonDocument.Parse(json);
                var root = jsonDoc.RootElement;
                var eventType = root.GetProperty("eventType").GetString();
                var correlationId = root.GetProperty("correlationId").GetString() ?? "unknown";
                var eventData = root.GetProperty("eventData");

                // Use a scope to get services
                using var scope = _serviceProvider.CreateAsyncScope();
                var projectionWriter = scope.ServiceProvider.GetRequiredService<IProductProjectionWriter>();

                // Process event based on type
                switch (eventType)
                {
                    case "ProductCreated":
                        {
                            var @event = JsonSerializer.Deserialize<ProductCreatedEvent>(eventData.GetRawText())
                                ?? throw new InvalidOperationException("Failed to deserialize ProductCreatedEvent");
                            await projectionWriter.HandleProductCreatedAsync(@event, correlationId, cancellationToken).ConfigureAwait(false);
                            _logger.LogInformation("Processed ProductCreated event for {ProductId}", @event.ProductId);
                            break;
                        }
                    case "ProductUpdated":
                        {
                            var @event = JsonSerializer.Deserialize<ProductUpdatedEvent>(eventData.GetRawText())
                                ?? throw new InvalidOperationException("Failed to deserialize ProductUpdatedEvent");
                            await projectionWriter.HandleProductUpdatedAsync(@event, correlationId, cancellationToken).ConfigureAwait(false);
                            _logger.LogInformation("Processed ProductUpdated event for {ProductId}", @event.ProductId);
                            break;
                        }
                    case "ProductActivated":
                        {
                            var @event = JsonSerializer.Deserialize<ProductActivatedEvent>(eventData.GetRawText())
                                ?? throw new InvalidOperationException("Failed to deserialize ProductActivatedEvent");
                            await projectionWriter.HandleProductActivatedAsync(@event, correlationId, cancellationToken).ConfigureAwait(false);
                            _logger.LogInformation("Processed ProductActivated event for {ProductId}", @event.ProductId);
                            break;
                        }
                    case "ProductDeactivated":
                        {
                            var @event = JsonSerializer.Deserialize<ProductDeactivatedEvent>(eventData.GetRawText())
                                ?? throw new InvalidOperationException("Failed to deserialize ProductDeactivatedEvent");
                            await projectionWriter.HandleProductDeactivatedAsync(@event, correlationId, cancellationToken).ConfigureAwait(false);
                            _logger.LogInformation("Processed ProductDeactivated event for {ProductId}", @event.ProductId);
                            break;
                        }
                    default:
                        _logger.LogWarning("Unknown event type: {EventType}", eventType);
                        break;
                }

                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(
            queue: "productcatalog.projections",
            autoAck: false,
            consumerTag: "product-event-consumer",
            consumer: consumer);

        _logger.LogInformation("Consuming events from productcatalog.projections queue...");

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
