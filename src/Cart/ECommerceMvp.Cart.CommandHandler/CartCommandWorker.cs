using ECommerceMvp.Cart.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace ECommerceMvp.Cart.CommandHandler;

public class CartCommandWorker : BackgroundService
{
    private readonly ILogger<CartCommandWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _rabbitMqHostName;
    private IConnection? _connection;
    private IChannel? _channel;

    public CartCommandWorker(ILogger<CartCommandWorker> logger, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _rabbitMqHostName = configuration.GetConnectionString("RabbitMQ") ?? "localhost";
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CartCommandWorker starting...");
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

            // Declare queue
            await _channel.QueueDeclareAsync(
                queue: "cart.commands",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // QoS: process one message at a time
            await _channel.BasicQosAsync(0, 1, false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += HandleMessageAsync;

            await _channel.BasicConsumeAsync(
                queue: "cart.commands",
                autoAck: false,
                consumerTag: "cart-command-worker",
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: consumer);

            _logger.LogInformation("CartCommandWorker connected and listening for commands");
            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting CartCommandWorker");
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
            _logger.LogInformation("Received command: {Message}", message);

            var commandMessage = JsonSerializer.Deserialize<CommandMessage>(message);
            if (commandMessage == null)
            {
                _logger.LogError("Failed to deserialize command message");
                return;
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                await ProcessCommandAsync(commandMessage, scope.ServiceProvider);
            }

            await _channel!.BasicAckAsync(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling command message");
            await _channel!.BasicNackAsync(args.DeliveryTag, false, true); // Requeue on error
        }
    }

    private async Task ProcessCommandAsync(CommandMessage commandMessage, IServiceProvider serviceProvider)
    {
        switch (commandMessage.CommandType)
        {
            case "CreateCartCommand":
                {
                    var command = JsonSerializer.Deserialize<CreateCartCommand>(commandMessage.Payload.ToString()!);
                    var handler = serviceProvider.GetRequiredService<ICommandHandler<CreateCartCommand, CreateCartResponse>>();
                    await handler.Handle(command!);
                    break;
                }
            case "AddCartItemCommand":
                {
                    var command = JsonSerializer.Deserialize<AddCartItemCommand>(commandMessage.Payload.ToString()!);
                    var handler = serviceProvider.GetRequiredService<ICommandHandler<AddCartItemCommand, AddCartItemResponse>>();
                    await handler.Handle(command!);
                    break;
                }
            case "UpdateCartItemQtyCommand":
                {
                    var command = JsonSerializer.Deserialize<UpdateCartItemQtyCommand>(commandMessage.Payload.ToString()!);
                    var handler = serviceProvider.GetRequiredService<ICommandHandler<UpdateCartItemQtyCommand, UpdateCartItemQtyResponse>>();
                    await handler.Handle(command!);
                    break;
                }
            case "RemoveCartItemCommand":
                {
                    var command = JsonSerializer.Deserialize<RemoveCartItemCommand>(commandMessage.Payload.ToString()!);
                    var handler = serviceProvider.GetRequiredService<ICommandHandler<RemoveCartItemCommand, RemoveCartItemResponse>>();
                    await handler.Handle(command!);
                    break;
                }
            case "ClearCartCommand":
                {
                    var command = JsonSerializer.Deserialize<ClearCartCommand>(commandMessage.Payload.ToString()!);
                    var handler = serviceProvider.GetRequiredService<ICommandHandler<ClearCartCommand, ClearCartResponse>>();
                    await handler.Handle(command!);
                    break;
                }
            default:
                _logger.LogWarning("Unknown command type: {CommandType}", commandMessage.CommandType);
                break;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CartCommandWorker stopping...");
        if (_channel != null)
            await _channel.CloseAsync();
        if (_connection != null)
            await _connection.CloseAsync();
        await base.StopAsync(cancellationToken);
    }

    private class CommandMessage
    {
        public string CommandType { get; set; } = null!;
        public JsonElement Payload { get; set; }
    }
}
