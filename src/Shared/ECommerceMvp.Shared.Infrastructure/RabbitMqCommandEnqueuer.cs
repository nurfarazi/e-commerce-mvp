using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ECommerceMvp.Shared.Infrastructure;

/// <summary>
/// Interface for enqueuing commands to RabbitMQ.
/// </summary>
public interface ICommandEnqueuer
{
    Task EnqueueAsync<TCommand>(TCommand command, string queueName, CancellationToken cancellationToken = default) where TCommand : class;
}

/// <summary>
/// Command enqueuer implementation using RabbitMQ.
/// </summary>
public class RabbitMqCommandEnqueuer : ICommandEnqueuer
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqCommandEnqueuer> _logger;

    public RabbitMqCommandEnqueuer(
        RabbitMqOptions options,
        ILogger<RabbitMqCommandEnqueuer> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _logger.LogInformation("RabbitMQ command enqueuer initialized");
    }

    public async Task EnqueueAsync<TCommand>(TCommand command, string queueName, CancellationToken cancellationToken = default) where TCommand : class
    {
        await Task.Run(() =>
        {
            // Declare queue
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var messageBody = JsonSerializer.Serialize(new
            {
                CommandId = Guid.NewGuid(),
                CommandType = typeof(TCommand).Name,
                Payload = command,
                EnqueuedAt = DateTimeOffset.UtcNow
            });

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";

            _channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                basicProperties: properties,
                body: System.Text.Encoding.UTF8.GetBytes(messageBody));

            _logger.LogInformation("Command {CommandType} enqueued to {QueueName}", typeof(TCommand).Name, queueName);
        }, cancellationToken);
    }
}
