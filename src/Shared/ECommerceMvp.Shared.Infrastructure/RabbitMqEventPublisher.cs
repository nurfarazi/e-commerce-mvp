using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace ECommerceMvp.Shared.Infrastructure;

/// <summary>
/// Configuration for RabbitMQ connection.
/// </summary>
public class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
}

/// <summary>
/// Event publisher implementation using RabbitMQ.
/// </summary>
public class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(
        RabbitMqOptions options,
        ILogger<RabbitMqEventPublisher> logger)
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

        _logger.LogInformation("RabbitMQ event publisher initialized");
    }

    public async Task PublishAsync(
        IEnumerable<DomainEventEnvelope> eventEnvelopes,
        CancellationToken cancellationToken = default)
    {
        var envelopes = eventEnvelopes.ToList();
        if (envelopes.Count == 0)
            return;

        await Task.Run(() =>
        {
            foreach (var envelope in envelopes)
            {
                // Extract bounded context from event type (e.g., "ECommerceMvp.ProductCatalog.Domain.ProductCreatedEvent" -> "ProductCatalog")
                var eventTypeParts = envelope.Event.EventType.Split('.');
                var boundedContext = eventTypeParts.Length > 1 ? eventTypeParts[1] : eventTypeParts[0];
                var exchangeName = $"{boundedContext}.events";
                var routingKey = envelope.Event.EventType;

                // Declare fanout exchange
                _channel.ExchangeDeclare(
                    exchange: exchangeName,
                    type: ExchangeType.Fanout,
                    durable: true,
                    autoDelete: false);

                // Serialize payload separately with concrete type to preserve all properties
                var payloadJson = JsonSerializer.Serialize(envelope.Event, envelope.Event.GetType());
                using var payloadDoc = JsonDocument.Parse(payloadJson);

                var messageBody = JsonSerializer.Serialize(new
                {
                    EventId = envelope.Event.EventId,
                    EventType = envelope.Event.EventType,
                    EventVersion = envelope.Event.EventVersion,
                    Payload = payloadDoc.RootElement,
                    Metadata = new
                    {
                        CorrelationId = envelope.CorrelationId,
                        CausationId = envelope.CausationId,
                        TenantId = envelope.TenantId,
                        UserId = envelope.UserId,
                        PublishedAt = DateTimeOffset.UtcNow
                    }
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";
                properties.Headers = new Dictionary<string, object?>
                {
                    ["EventId"] = envelope.Event.EventId,
                    ["CorrelationId"] = envelope.CorrelationId,
                    ["CausationId"] = envelope.CausationId,
                    ["TenantId"] = envelope.TenantId,
                    ["UserId"] = envelope.UserId
                };

                _channel.BasicPublish(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: Encoding.UTF8.GetBytes(messageBody));

                _logger.LogDebug(
                    "Published event {EventType} EventId={EventId} to exchange {ExchangeName}",
                    envelope.Event.EventType, envelope.Event.EventId, exchangeName);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
