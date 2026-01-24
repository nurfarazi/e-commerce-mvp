using ECommerceMvp.Checkout.CommandHandler;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Checkout.Application;
using ECommerceMvp.Checkout.Infrastructure;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register core services
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        services.AddSingleton<ICommandEnqueuer, RabbitMqCommandEnqueuer>();

        // Register Checkout services
        services.AddSingleton(typeof(IRepository<,>), typeof(CheckoutRepository));
        services.AddSingleton(typeof(IIdempotencyStore), typeof(InMemoryIdempotencyStore));

        // Register command handlers
        services.AddScoped(typeof(ICommandHandler<,>), typeof(InitiateCheckoutCommandHandler));
        services.AddScoped(typeof(ICommandHandler<,>), typeof(AdvanceSagaCommandHandler));

        // Register the command worker
        services.AddHostedService<CheckoutCommandWorker>();
    })
    .Build();

await host.RunAsync();

// In-memory idempotency store implementation
public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, string> _processed = [];
    private readonly Dictionary<string, object?> _commandResults = [];
    private readonly HashSet<string> _processedEvents = [];

    public async Task<IdempotencyCheckResult> CheckIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (_processed.TryGetValue(idempotencyKey, out var aggregateId))
        {
            return new IdempotencyCheckResult { IsIdempotent = true, AggregateId = aggregateId };
        }
        return new IdempotencyCheckResult { IsIdempotent = false };
    }

    public async Task<bool> IsCommandProcessedAsync(string commandId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_commandResults.ContainsKey(commandId)).ConfigureAwait(false);
    }

    public async Task<T?> GetCommandResultAsync<T>(string commandId, CancellationToken cancellationToken = default)
    {
        if (_commandResults.TryGetValue(commandId, out var result))
        {
            if (result is T typedResult)
                return await Task.FromResult(typedResult).ConfigureAwait(false);

            if (result == null)
                return await Task.FromResult<T?>(default).ConfigureAwait(false);
        }

        return await Task.FromResult<T?>(default).ConfigureAwait(false);
    }

    public async Task MarkCommandAsProcessedAsync(string commandId, object result, CancellationToken cancellationToken = default)
    {
        _commandResults[commandId] = result;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<bool> IsEventProcessedAsync(string eventId, string handlerName, CancellationToken cancellationToken = default)
    {
        var key = $"{eventId}:{handlerName}";
        return await Task.FromResult(_processedEvents.Contains(key)).ConfigureAwait(false);
    }

    public async Task MarkEventAsProcessedAsync(string eventId, string handlerName, CancellationToken cancellationToken = default)
    {
        var key = $"{eventId}:{handlerName}";
        _processedEvents.Add(key);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task MarkIdempotencyProcessedAsync(string idempotencyKey, string aggregateId, CancellationToken cancellationToken = default)
    {
        _processed[idempotencyKey] = aggregateId;
        await Task.CompletedTask;
    }

    public async Task<InitiateCheckoutResponse?> GetResultAsync(string idempotencyKey)
    {
        if (_commandResults.TryGetValue(idempotencyKey, out var result))
        {
            return result as InitiateCheckoutResponse;
        }
        return null;
    }

    public async Task StoreResultAsync(string idempotencyKey, InitiateCheckoutResponse response)
    {
        _commandResults[idempotencyKey] = response;
    }
}
