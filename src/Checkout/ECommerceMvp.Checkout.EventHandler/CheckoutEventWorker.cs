using ECommerceMvp.Shared.Application;
using ECommerceMvp.Checkout.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace ECommerceMvp.Checkout.EventHandler;

/// <summary>
/// Background worker that processes events from other bounded contexts.
/// Subscribes to:
///   - Cart: CartSnapshotProvidedEvent, CartClearedEvent, CartSnapshotFailedEvent
///   - ProductCatalog: ProductSnapshotsProvidedEvent, ProductSnapshotFailedEvent
///   - Inventory: StockBatchValidatedEvent, StockDeductedForOrderEvent
///   - Order: OrderCreatedEvent, OrderFinalizedEvent
/// Forwards events as AdvanceSagaCommand to Checkout CommandHandler.
/// </summary>
public class CheckoutEventWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CheckoutEventWorker> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public CheckoutEventWorker(IServiceProvider serviceProvider, ILogger<CheckoutEventWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CheckoutEventWorker starting...");

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
                Port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var port) ? port : 5672,
                UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
                Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest"
            };

            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Declare exchanges
            await _channel.ExchangeDeclareAsync("cart.events", "fanout", durable: true, cancellationToken: stoppingToken);
            await _channel.ExchangeDeclareAsync("productcatalog.events", "fanout", durable: true, cancellationToken: stoppingToken);
            await _channel.ExchangeDeclareAsync("inventory.events", "fanout", durable: true, cancellationToken: stoppingToken);
            await _channel.ExchangeDeclareAsync("order.events", "fanout", durable: true, cancellationToken: stoppingToken);

            // Declare queue
            var queueName = await _channel.QueueDeclareAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
            var queue = queueName.QueueNameAsString;

            // Bind to all events we care about
            await _channel.QueueBindAsync(queue, "cart.events", "", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(queue, "productcatalog.events", "", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(queue, "inventory.events", "", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(queue, "order.events", "", cancellationToken: stoppingToken);

            // Set QoS
            await _channel.BasicQosAsync(0, 1, false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    await ProcessEventAsync(ea, stoppingToken);
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: queue,
                autoAck: false,
                consumerTag: "checkout-event-consumer",
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("CheckoutEventWorker started successfully");

            // Keep the worker running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CheckoutEventWorker cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in CheckoutEventWorker");
        }
    }

    private async Task ProcessEventAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var body = ea.Body.ToArray();
        var message = System.Text.Encoding.UTF8.GetString(body);

        _logger.LogDebug("Processing event: {Message}", message);

        var eventData = JsonDocument.Parse(message).RootElement;
        var eventType = eventData.GetProperty("eventType").GetString() ?? "";

        // Extract CheckoutId from event (must be present)
        string checkoutId = string.Empty;
        if (eventData.TryGetProperty("checkoutId", out var checkoutIdProp))
        {
            checkoutId = checkoutIdProp.GetString() ?? string.Empty;
        }

        // Ignore events without CheckoutId (not for this saga)
        if (string.IsNullOrEmpty(checkoutId))
        {
            _logger.LogDebug("Ignoring event without CheckoutId: {EventType}", eventType);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        // Route event to saga advancement
        var shouldAdvance = eventType switch
        {
            "CartSnapshotProvidedEvent" or
            "CartSnapshotFailedEvent" or
            "ProductSnapshotsProvidedEvent" or
            "ProductSnapshotFailedEvent" or
            "StockBatchValidatedEvent" or
            "StockDeductedForOrderEvent" or
            "OrderCreatedEvent" or
            "CartClearedEvent" or
            "OrderFinalizedEvent" => true,
            _ => false
        };

        if (!shouldAdvance)
        {
            _logger.LogWarning("Unknown event type for saga: {EventType}", eventType);
            return;
        }

        // Extract event payload
        object? eventPayload = null;
        eventPayload = eventType switch
        {
            "CartSnapshotProvidedEvent" => ExtractCartItems(eventData),
            "ProductSnapshotsProvidedEvent" => ExtractProductSnapshots(eventData),
            "StockBatchValidatedEvent" => ExtractStockValidation(eventData),
            "OrderCreatedEvent" => ExtractOrderCreatedInfo(eventData),
            _ => null
        };

        var command = new AdvanceSagaCommand
        {
            CheckoutId = checkoutId,
            EventType = eventType,
            EventPayload = eventPayload ?? new object()
        };

        try
        {
            var response = await dispatcher.DispatchAsync<AdvanceSagaCommand, AdvanceSagaResponse>(command);
            _logger.LogInformation("Saga advanced for {CheckoutId} with event {EventType}",
                checkoutId, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error advancing saga for event {EventType}", eventType);
        }
    }

    private List<CartItemSnapshotDto> ExtractCartItems(JsonElement eventData)
    {
        var items = new List<CartItemSnapshotDto>();
        if (eventData.TryGetProperty("cartItems", out var cartItemsProp))
        {
            foreach (var item in cartItemsProp.EnumerateArray())
            {
                items.Add(new CartItemSnapshotDto
                {
                    ProductId = item.GetProperty("productId").GetString() ?? string.Empty,
                    Quantity = item.GetProperty("quantity").GetInt32()
                });
            }
        }
        return items;
    }

    private List<ProductSnapshotDto> ExtractProductSnapshots(JsonElement eventData)
    {
        var products = new List<ProductSnapshotDto>();
        if (eventData.TryGetProperty("productSnapshots", out var productsProp))
        {
            foreach (var product in productsProp.EnumerateArray())
            {
                products.Add(new ProductSnapshotDto
                {
                    ProductId = product.GetProperty("productId").GetString() ?? string.Empty,
                    Sku = product.GetProperty("sku").GetString() ?? string.Empty,
                    Name = product.GetProperty("name").GetString() ?? string.Empty,
                    Price = product.GetProperty("price").GetDecimal(),
                    Currency = product.GetProperty("currency").GetString() ?? "USD",
                    IsActive = product.GetProperty("isActive").GetBoolean()
                });
            }
        }
        return products;
    }

    private StockValidationResult? ExtractStockValidation(JsonElement eventData)
    {
        var result = new StockValidationResult();
        if (eventData.TryGetProperty("allAvailable", out var allAvailableProp))
        {
            result.AllAvailable = allAvailableProp.GetBoolean();
        }
        if (eventData.TryGetProperty("results", out var resultsProp))
        {
            foreach (var item in resultsProp.EnumerateArray())
            {
                result.Results.Add(new StockValidationResultDto
                {
                    ProductId = item.GetProperty("productId").GetString() ?? string.Empty,
                    RequestedQuantity = item.GetProperty("requestedQuantity").GetInt32(),
                    AvailableQuantity = item.GetProperty("availableQuantity").GetInt32(),
                    IsAvailable = item.GetProperty("isAvailable").GetBoolean()
                });
            }
        }
        return result;
    }

    private OrderCreatedInfo? ExtractOrderCreatedInfo(JsonElement eventData)
    {
        return new OrderCreatedInfo
        {
            OrderNumber = eventData.GetProperty("orderNumber").GetString() ?? string.Empty
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CheckoutEventWorker stopping...");

        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync(cancellationToken);
            _connection.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}
