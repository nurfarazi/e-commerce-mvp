using ECommerceMvp.Shared.Application;
using ECommerceMvp.Checkout.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace ECommerceMvp.Checkout.CommandHandler;

/// <summary>
/// Background worker that processes Checkout commands from RabbitMQ.
/// Listens to: checkout.commands queue
/// </summary>
public class CheckoutCommandWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CheckoutCommandWorker> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public CheckoutCommandWorker(IServiceProvider serviceProvider, ILogger<CheckoutCommandWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CheckoutCommandWorker starting...");

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

            // Declare queue
            await _channel.QueueDeclareAsync(
                queue: "checkout.commands",
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            // Set QoS
            await _channel.BasicQosAsync(0, 1, false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    await ProcessCommandAsync(ea, stoppingToken);
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing command");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: "checkout.commands",
                autoAck: false,
                consumerTag: "checkout-command-consumer",
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("CheckoutCommandWorker started successfully");

            // Keep the worker running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CheckoutCommandWorker cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in CheckoutCommandWorker");
        }
    }

    private async Task ProcessCommandAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var body = ea.Body.ToArray();
        var message = System.Text.Encoding.UTF8.GetString(body);

        _logger.LogDebug("Processing Checkout command: {Message}", message);

        using var scope = _serviceProvider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var commandData = JsonDocument.Parse(message).RootElement;
        var commandType = commandData.GetProperty("commandType").GetString();

        switch (commandType)
        {
            case "InitiateCheckout":
                await HandleInitiateCheckout(commandData, dispatcher);
                break;

            case "AdvanceSaga":
                await HandleAdvanceSaga(commandData, dispatcher);
                break;

            default:
                _logger.LogWarning("Unknown command type: {CommandType}", commandType);
                break;
        }
    }

    private async Task HandleInitiateCheckout(JsonElement commandData, ICommandDispatcher dispatcher)
    {
        var command = new InitiateCheckoutCommand
        {
            CheckoutId = commandData.GetProperty("checkoutId").GetString() ?? string.Empty,
            OrderId = commandData.GetProperty("orderId").GetString() ?? string.Empty,
            GuestToken = commandData.GetProperty("guestToken").GetString() ?? string.Empty,
            CartId = commandData.GetProperty("cartId").GetString() ?? string.Empty,
            IdempotencyKey = commandData.GetProperty("idempotencyKey").GetString() ?? string.Empty
        };

        try
        {
            var response = await dispatcher.DispatchAsync<InitiateCheckoutCommand, InitiateCheckoutResponse>(command);
            _logger.LogInformation("InitiateCheckout processed: {CheckoutId}", command.CheckoutId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling InitiateCheckout");
        }
    }

    private async Task HandleAdvanceSaga(JsonElement commandData, ICommandDispatcher dispatcher)
    {
        var command = new AdvanceSagaCommand
        {
            CheckoutId = commandData.GetProperty("checkoutId").GetString() ?? string.Empty,
            EventType = commandData.GetProperty("eventType").GetString() ?? string.Empty,
            EventPayload = commandData.GetProperty("eventPayload")
        };

        try
        {
            var response = await dispatcher.DispatchAsync<AdvanceSagaCommand, AdvanceSagaResponse>(command);
            _logger.LogInformation("AdvanceSaga processed: {CheckoutId}", command.CheckoutId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling AdvanceSaga");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CheckoutCommandWorker stopping...");

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
