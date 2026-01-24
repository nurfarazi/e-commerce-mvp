using ECommerceMvp.Cart.Application;
using ECommerceMvp.Cart.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace ECommerceMvp.Cart.EventHandler;

public class CartEventWorker : BackgroundService
{
    private readonly ILogger<CartEventWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _rabbitMqHostName;
    private IConnection? _connection;
    private IChannel? _channel;

    public CartEventWorker(ILogger<CartEventWorker> logger, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _rabbitMqHostName = configuration.GetConnectionString("RabbitMQ") ?? "localhost";
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CartEventWorker starting...");
        await Task.Delay(2000, cancellationToken); // Wait for RabbitMQ to be ready

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqHostName,
                UserName = "guest",
                Password = "guest",
                AutomaticRecoveryEnabled = true
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            // Declare fanout exchange
            await _channel.ExchangeDeclareAsync(
                exchange: "Cart.events",
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                arguments: null);

            // Declare queue
            await _channel.QueueDeclareAsync(
                queue: "cart.projections",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Bind queue to exchange
            await _channel.QueueBindAsync(
                queue: "cart.projections",
                exchange: "Cart.events",
                routingKey: "",
                arguments: null);

            // QoS: process one message at a time
            await _channel.BasicQosAsync(0, 1, false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += HandleMessageAsync;

            await _channel.BasicConsumeAsync(
                queue: "cart.projections",
                autoAck: false,
                consumerTag: "cart-event-worker",
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: consumer);

            _logger.LogInformation("CartEventWorker connected and listening for events");
            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting CartEventWorker");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task HandleMessageAsync(object model, BasicDeliverEventArgs args)
    {
        try
        {
            var body = args.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            _logger.LogInformation("Received event: {Message}", message);

            var eventEnvelope = JsonSerializer.Deserialize<EventEnvelope>(message);
            if (eventEnvelope == null)
            {
                _logger.LogError("Failed to deserialize event");
                return;
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                await ProcessEventAsync(eventEnvelope, scope.ServiceProvider);
            }

            await _channel!.BasicAckAsync(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling event message");
            await _channel!.BasicNackAsync(args.DeliveryTag, false, true); // Requeue on error
        }
    }

    private async Task ProcessEventAsync(EventEnvelope eventEnvelope, IServiceProvider serviceProvider)
    {
        var projectionWriter = serviceProvider.GetRequiredService<ICartProjectionWriter>();

        switch (eventEnvelope.EventType)
        {
            case "ECommerceMvp.Cart.Domain.CartCreatedEvent":
                {
                    var @event = JsonSerializer.Deserialize<CartCreatedEvent>(eventEnvelope.Payload.ToString()!);
                    if (@event != null)
                        await projectionWriter.HandleCartCreatedAsync(@event);
                    break;
                }
            case "ECommerceMvp.Cart.Domain.CartItemAddedEvent":
                {
                    var @event = JsonSerializer.Deserialize<CartItemAddedEvent>(eventEnvelope.Payload.ToString()!);
                    if (@event != null)
                        await projectionWriter.HandleCartItemAddedAsync(@event);
                    break;
                }
            case "ECommerceMvp.Cart.Domain.CartItemQuantityUpdatedEvent":
                {
                    var @event = JsonSerializer.Deserialize<CartItemQuantityUpdatedEvent>(eventEnvelope.Payload.ToString()!);
                    if (@event != null)
                        await projectionWriter.HandleCartItemQuantityUpdatedAsync(@event);
                    break;
                }
            case "ECommerceMvp.Cart.Domain.CartItemRemovedEvent":
                {
                    var @event = JsonSerializer.Deserialize<CartItemRemovedEvent>(eventEnvelope.Payload.ToString()!);
                    if (@event != null)
                        await projectionWriter.HandleCartItemRemovedAsync(@event);
                    break;
                }
            case "ECommerceMvp.Cart.Domain.CartClearedEvent":
                {
                    var @event = JsonSerializer.Deserialize<CartClearedEvent>(eventEnvelope.Payload.ToString()!);
                    if (@event != null)
                        await projectionWriter.HandleCartClearedAsync(@event);
                    break;
                }
            default:
                _logger.LogWarning("Unknown event type: {EventType}", eventEnvelope.EventType);
                break;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CartEventWorker stopping...");
        if (_channel != null)
            await _channel.CloseAsync();
        if (_connection != null)
            await _connection.CloseAsync();
        await base.StopAsync(cancellationToken);
    }

    private class EventEnvelope
    {
        public string EventId { get; set; } = null!;
        public string EventType { get; set; } = null!;
        public int EventVersion { get; set; }
        public JsonElement Payload { get; set; }
        public JsonElement Metadata { get; set; }
    }
}
