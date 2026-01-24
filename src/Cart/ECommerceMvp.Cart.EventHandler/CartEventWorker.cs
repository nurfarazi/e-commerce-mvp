using ECommerceMvp.Cart.Application;
using ECommerceMvp.Cart.Domain;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Infrastructure;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace ECommerceMvp.Cart.EventHandler;

/// <summary>
/// Background worker that consumes domain events from RabbitMQ and projects them.
/// </summary>
public class CartEventWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ILogger<CartEventWorker> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public CartEventWorker(
        IServiceProvider serviceProvider,
        RabbitMqOptions rabbitMqOptions,
        ILogger<CartEventWorker> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _rabbitMqOptions = rabbitMqOptions ?? throw new ArgumentNullException(nameof(rabbitMqOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CartEventWorker starting...");

        try
        {
            await ConnectToRabbitMqAsync(stoppingToken).ConfigureAwait(false);
            await ConsumeEventsAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CartEventWorker stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CartEventWorker encountered an error");
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

        // Declare fanout exchange for Cart events
        _channel.ExchangeDeclare(
            exchange: "Cart.events",
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false);

        // Declare queue for projections
        _channel.QueueDeclare(
            queue: "cart.events",
            durable: true,
            exclusive: false,
            autoDelete: false);

        // Bind queue to exchange
        _channel.QueueBind(
            queue: "cart.events",
            exchange: "Cart.events",
            routingKey: "");

        _channel.BasicQos(0, 1, false);

        _logger.LogInformation("Connected to RabbitMQ");

        await Task.CompletedTask.ConfigureAwait(false);
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

                using (var scope = _serviceProvider.CreateScope())
                {
                    try
                    {
                        var eventDoc = JsonDocument.Parse(json);
                        var root = eventDoc.RootElement;
                        var eventType = root.GetProperty("EventType").GetString();

                        if (string.IsNullOrEmpty(eventType))
                        {
                            _logger.LogWarning("Event type is missing in event message");
                            _channel.BasicNack(ea.DeliveryTag, false, false);
                            return;
                        }

                        var payload = root.GetProperty("Payload");
                        var projectionWriter = scope.ServiceProvider.GetRequiredService<ICartProjectionWriter>();

                        // Route to appropriate event handler based on event type
                        if (eventType == "ECommerceMvp.Cart.Domain.CartCreatedEvent")
                        {
                            var @event = JsonSerializer.Deserialize<CartCreatedEvent>(payload.GetRawText());
                            if (@event != null)
                                await projectionWriter.HandleCartCreatedAsync(@event).ConfigureAwait(false);
                        }
                        else if (eventType == "ECommerceMvp.Cart.Domain.CartItemAddedEvent")
                        {
                            var @event = JsonSerializer.Deserialize<CartItemAddedEvent>(payload.GetRawText());
                            if (@event != null)
                                await projectionWriter.HandleCartItemAddedAsync(@event).ConfigureAwait(false);
                        }
                        else if (eventType == "ECommerceMvp.Cart.Domain.CartItemQuantityUpdatedEvent")
                        {
                            var @event = JsonSerializer.Deserialize<CartItemQuantityUpdatedEvent>(payload.GetRawText());
                            if (@event != null)
                                await projectionWriter.HandleCartItemQuantityUpdatedAsync(@event).ConfigureAwait(false);
                        }
                        else if (eventType == "ECommerceMvp.Cart.Domain.CartItemRemovedEvent")
                        {
                            var @event = JsonSerializer.Deserialize<CartItemRemovedEvent>(payload.GetRawText());
                            if (@event != null)
                                await projectionWriter.HandleCartItemRemovedAsync(@event).ConfigureAwait(false);
                        }
                        else if (eventType == "ECommerceMvp.Cart.Domain.CartClearedEvent")
                        {
                            var @event = JsonSerializer.Deserialize<CartClearedEvent>(payload.GetRawText());
                            if (@event != null)
                                await projectionWriter.HandleCartClearedAsync(@event).ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.LogWarning("Unknown event type: {EventType}", eventType);
                            _channel.BasicNack(ea.DeliveryTag, false, false);
                            return;
                        }

                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing event");
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in message handler");
            }
        };

        _channel.BasicConsume(
            queue: "cart.events",
            autoAck: false,
            consumerTag: "cart-event-worker",
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer: consumer);

        _logger.LogInformation("CartEventWorker listening for events");

        // Keep the consumer alive
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }
    }

    public override void Dispose()
    {
        _logger.LogInformation("CartEventWorker stopping...");
        if (_channel != null)
        {
            try { _channel.Close(); } catch { }
            _channel.Dispose();
        }
        if (_connection != null)
        {
            try { _connection.Close(); } catch { }
            _connection.Dispose();
        }
        base.Dispose();
    }
}
