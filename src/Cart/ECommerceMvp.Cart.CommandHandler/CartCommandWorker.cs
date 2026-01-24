using ECommerceMvp.Cart.Application;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Infrastructure;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace ECommerceMvp.Cart.CommandHandler;

/// <summary>
/// Background worker that consumes commands from RabbitMQ and executes them.
/// </summary>
public class CartCommandWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ILogger<CartCommandWorker> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public CartCommandWorker(
        IServiceProvider serviceProvider,
        RabbitMqOptions rabbitMqOptions,
        ILogger<CartCommandWorker> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _rabbitMqOptions = rabbitMqOptions ?? throw new ArgumentNullException(nameof(rabbitMqOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CartCommandWorker starting...");

        try
        {
            await ConnectToRabbitMqAsync(stoppingToken).ConfigureAwait(false);
            await ConsumeCommandsAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CartCommandWorker stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CartCommandWorker encountered an error");
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
            queue: "cart.commands",
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

                using (var scope = _serviceProvider.CreateScope())
                {
                    try
                    {
                        var commandDoc = JsonDocument.Parse(json);
                        var root = commandDoc.RootElement;
                        var commandType = root.GetProperty("CommandType").GetString();

                        if (string.IsNullOrEmpty(commandType))
                        {
                            _logger.LogWarning("Command type is missing in command message");
                            _channel.BasicNack(ea.DeliveryTag, false, false);
                            return;
                        }

                        var payload = root.GetProperty("Payload");

                        // Route to appropriate handler based on command type
                        if (commandType == "CreateCartCommand")
                        {
                            var cmd = JsonSerializer.Deserialize<CreateCartCommand>(payload.GetRawText());
                            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateCartCommand, CreateCartResponse>>();
                            var result = await handler.HandleAsync(cmd!, CancellationToken.None).ConfigureAwait(false);

                            _logger.LogInformation("CreateCartCommand processed");
                        }
                        else if (commandType == "AddCartItemCommand")
                        {
                            var cmd = JsonSerializer.Deserialize<AddCartItemCommand>(payload.GetRawText());
                            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<AddCartItemCommand, AddCartItemResponse>>();
                            var result = await handler.HandleAsync(cmd!, CancellationToken.None).ConfigureAwait(false);

                            _logger.LogInformation("AddCartItemCommand processed");
                        }
                        else if (commandType == "UpdateCartItemQtyCommand")
                        {
                            var cmd = JsonSerializer.Deserialize<UpdateCartItemQtyCommand>(payload.GetRawText());
                            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<UpdateCartItemQtyCommand, UpdateCartItemQtyResponse>>();
                            var result = await handler.HandleAsync(cmd!, CancellationToken.None).ConfigureAwait(false);

                            _logger.LogInformation("UpdateCartItemQtyCommand processed");
                        }
                        else if (commandType == "RemoveCartItemCommand")
                        {
                            var cmd = JsonSerializer.Deserialize<RemoveCartItemCommand>(payload.GetRawText());
                            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RemoveCartItemCommand, RemoveCartItemResponse>>();
                            var result = await handler.HandleAsync(cmd!, CancellationToken.None).ConfigureAwait(false);

                            _logger.LogInformation("RemoveCartItemCommand processed");
                        }
                        else if (commandType == "ClearCartCommand")
                        {
                            var cmd = JsonSerializer.Deserialize<ClearCartCommand>(payload.GetRawText());
                            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ClearCartCommand, ClearCartResponse>>();
                            var result = await handler.HandleAsync(cmd!, CancellationToken.None).ConfigureAwait(false);

                            _logger.LogInformation("ClearCartCommand processed");
                        }
                        else
                        {
                            _logger.LogWarning("Unknown command type: {CommandType}", commandType);
                            _channel.BasicNack(ea.DeliveryTag, false, false);
                            return;
                        }

                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing command");
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
            queue: "cart.commands",
            autoAck: false,
            consumerTag: "cart-command-worker",
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer: consumer);

        _logger.LogInformation("CartCommandWorker listening for commands");

        // Keep the consumer alive
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }
    }

    public override void Dispose()
    {
        _logger.LogInformation("CartCommandWorker stopping...");
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
