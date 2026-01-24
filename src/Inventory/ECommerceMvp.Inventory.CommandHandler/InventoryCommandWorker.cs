using ECommerceMvp.Inventory.Application;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Infrastructure;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace ECommerceMvp.Inventory.CommandHandler;

/// <summary>
/// Background worker that consumes commands from RabbitMQ and executes them.
/// </summary>
public class InventoryCommandWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ILogger<InventoryCommandWorker> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public InventoryCommandWorker(
        IServiceProvider serviceProvider,
        RabbitMqOptions rabbitMqOptions,
        ILogger<InventoryCommandWorker> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _rabbitMqOptions = rabbitMqOptions ?? throw new ArgumentNullException(nameof(rabbitMqOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InventoryCommandWorker starting...");

        try
        {
            await ConnectToRabbitMqAsync(stoppingToken).ConfigureAwait(false);
            await ConsumeCommandsAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("InventoryCommandWorker stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InventoryCommandWorker encountered an error");
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
            queue: "inventory.commands",
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

                // Deserialize the command envelope
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
                        if (commandType == "SetStockCommand")
                        {
                            var cmd = JsonSerializer.Deserialize<SetStockCommand>(payload.GetRawText());
                            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<SetStockCommand, SetStockResponse>>();
                            var result = await handler.HandleAsync(cmd!, CancellationToken.None).ConfigureAwait(false);

                            _logger.LogInformation("SetStockCommand processed: {Result}", result.Success);
                        }
                        else if (commandType == "ValidateStockCommand")
                        {
                            var cmd = JsonSerializer.Deserialize<ValidateStockCommand>(payload.GetRawText());
                            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ValidateStockCommand, ValidateStockResponse>>();
                            var result = await handler.HandleAsync(cmd!, CancellationToken.None).ConfigureAwait(false);

                            _logger.LogInformation("ValidateStockCommand processed: {Result}", result.Success);
                        }
                        else if (commandType == "DeductStockForOrderCommand")
                        {
                            var cmd = JsonSerializer.Deserialize<DeductStockForOrderCommand>(payload.GetRawText());
                            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<DeductStockForOrderCommand, DeductStockForOrderResponse>>();
                            var result = await handler.HandleAsync(cmd!, CancellationToken.None).ConfigureAwait(false);

                            _logger.LogInformation("DeductStockForOrderCommand processed: {Result}", result.Success);
                        }
                        else
                        {
                            _logger.LogWarning("Unknown command type: {CommandType}", commandType);
                        }

                        // Acknowledge successful processing
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Invalid JSON in command message");
                        _channel.BasicNack(ea.DeliveryTag, false, false); // Don't requeue bad JSON
                    }
                    catch (KeyNotFoundException keyEx)
                    {
                        _logger.LogError(keyEx, "Missing required property in command message");
                        _channel.BasicNack(ea.DeliveryTag, false, false);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Recoverable error processing command");
                        _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue for retry
                    }
                }
                
                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in command consumer");
            }
        };

        _channel.BasicConsume(
            queue: "inventory.commands",
            autoAck: false,
            consumerTag: "inventory-command-consumer",
            consumer: consumer);

        _logger.LogInformation("Consuming commands from inventory.commands queue...");

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
