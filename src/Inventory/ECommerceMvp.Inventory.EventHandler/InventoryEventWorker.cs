using ECommerceMvp.Inventory.Application;
using ECommerceMvp.Inventory.Domain;
using ECommerceMvp.Inventory.Infrastructure;
using ECommerceMvp.Shared.Infrastructure;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace ECommerceMvp.Inventory.EventHandler;

/// <summary>
/// Background worker that consumes events from RabbitMQ and projects them to read models.
/// </summary>
public class InventoryEventWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ILogger<InventoryEventWorker> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public InventoryEventWorker(
        IServiceProvider serviceProvider,
        RabbitMqOptions rabbitMqOptions,
        ILogger<InventoryEventWorker> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _rabbitMqOptions = rabbitMqOptions ?? throw new ArgumentNullException(nameof(rabbitMqOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InventoryEventWorker starting...");

        try
        {
            await ConnectToRabbitMqAsync(stoppingToken).ConfigureAwait(false);
            await ConsumeEventsAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("InventoryEventWorker stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InventoryEventWorker encountered an error");
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

        // Declare fanout exchange for inventory events
        _channel.ExchangeDeclare(
            exchange: "Inventory.events",
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false);

        // Declare and bind queue
        _channel.QueueDeclare(
            queue: "inventory.projections",
            durable: true,
            exclusive: false,
            autoDelete: false);

        _channel.QueueBind(
            queue: "inventory.projections",
            exchange: "Inventory.events",
            routingKey: string.Empty);

        _channel.BasicQos(0, 1, false);

        _logger.LogInformation("Connected to RabbitMQ for Inventory events");
    }

    private async Task ConsumeEventsAsync(CancellationToken cancellationToken)
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

                _logger.LogDebug("Received event: {Json}", json);

                // Deserialize the event envelope
                using var jsonDoc = JsonDocument.Parse(json);
                var root = jsonDoc.RootElement;
                var eventTypeFullName = root.GetProperty("eventType").GetString();
                
                if (string.IsNullOrEmpty(eventTypeFullName))
                {
                    _logger.LogWarning("Event type is missing in event message");
                    _channel.BasicNack(ea.DeliveryTag, false, false);
                    return;
                }
                
                var correlationId = root.GetProperty("metadata").GetProperty("correlationId").GetString() ?? "unknown";
                var payloadElement = root.GetProperty("payload");

                // Extract simple event name from full type name
                var eventTypeName = eventTypeFullName.Split('.').Last().Replace("Event", "");

                // Use a scope to get services
                using var scope = _serviceProvider.CreateAsyncScope();
                var projectionWriter = scope.ServiceProvider.GetRequiredService<IInventoryProjectionWriter>();

                // Process event based on type
                switch (eventTypeName)
                {
                    case "StockItemCreated":
                        {
                            var @event = JsonSerializer.Deserialize<StockItemCreatedEvent>(payloadElement.GetRawText())
                                ?? throw new InvalidOperationException("Failed to deserialize StockItemCreatedEvent");
                            await projectionWriter.HandleStockItemCreatedAsync(@event, correlationId, cancellationToken).ConfigureAwait(false);
                            _logger.LogInformation("Processed StockItemCreated event for product {ProductId}", @event.ProductId);
                            break;
                        }
                    case "StockSet":
                        {
                            var @event = JsonSerializer.Deserialize<StockSetEvent>(payloadElement.GetRawText())
                                ?? throw new InvalidOperationException("Failed to deserialize StockSetEvent");
                            await projectionWriter.HandleStockSetAsync(@event, correlationId, cancellationToken).ConfigureAwait(false);
                            _logger.LogInformation("Processed StockSet event for product {ProductId}", @event.ProductId);
                            break;
                        }
                    case "StockDeductedForOrder":
                        {
                            var @event = JsonSerializer.Deserialize<StockDeductedForOrderEvent>(payloadElement.GetRawText())
                                ?? throw new InvalidOperationException("Failed to deserialize StockDeductedForOrderEvent");
                            await projectionWriter.HandleStockDeductedForOrderAsync(@event, correlationId, cancellationToken).ConfigureAwait(false);
                            _logger.LogInformation("Processed StockDeductedForOrder event for order {OrderId}", @event.OrderId);
                            break;
                        }
                    case "StockDeductionRejected":
                        {
                            var @event = JsonSerializer.Deserialize<StockDeductionRejectedEvent>(payloadElement.GetRawText())
                                ?? throw new InvalidOperationException("Failed to deserialize StockDeductionRejectedEvent");
                            await projectionWriter.HandleStockDeductionRejectedAsync(@event, correlationId, cancellationToken).ConfigureAwait(false);
                            _logger.LogWarning("Processed StockDeductionRejected event for order {OrderId}", @event.OrderId);
                            break;
                        }
                    default:
                        _logger.LogWarning("Unknown event type: {EventType}", eventTypeName);
                        break;
                }

                // Acknowledge successful processing
                _channel.BasicAck(ea.DeliveryTag, false);

                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(
            queue: "inventory.projections",
            autoAck: false,
            consumerTag: "inventory-event-consumer",
            consumer: consumer);

        _logger.LogInformation("Consuming events from inventory.projections queue...");

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
