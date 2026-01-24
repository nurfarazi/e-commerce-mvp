using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Order.Application;
using ECommerceMvp.Order.Infrastructure;
using ECommerceMvp.Order.QueryApi;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceMvp.Order.CommandApi;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddSwaggerGen();
        builder.Services.AddLogging();

        // Register core services
        builder.Services.AddSingleton<ICommandBus, CommandBus>();
        builder.Services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();

        // Register Order services
        builder.Services.AddSingleton(typeof(IRepository<,>), typeof(InMemoryOrderRepository<>));
        builder.Services.AddSingleton(typeof(IEventStore), typeof(InMemoryOrderEventStore));
        builder.Services.AddSingleton(typeof(IIdempotencyStore), typeof(InMemoryIdempotencyStore));
        builder.Services.AddSingleton(typeof(IReadModelStore<>), typeof(InMemoryReadModelStore<>));

        // Register command handlers
        builder.Services.AddScoped(typeof(ICommandHandler<,>), typeof(PlaceOrderCommandHandler));

        // Register query handlers  
        builder.Services.AddScoped(typeof(IQueryHandler<,>), typeof(GetOrderDetailQueryHandler));
        builder.Services.AddScoped(typeof(IQueryHandler<,>), typeof(GetOrderDetailByNumberQueryHandler));
        builder.Services.AddScoped(typeof(IQueryHandler<,>), typeof(GetAllOrdersAdminQueryHandler));

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}

// Generic repository wrapper if needed
public class InMemoryOrderRepository<T> : IRepository<T, string> where T : IAggregateRoot<string>
{
    private readonly Dictionary<string, T> _items = [];
    private readonly ILogger _logger;

    public InMemoryOrderRepository(ILogger<InMemoryOrderRepository<T>> logger)
    {
        _logger = logger;
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _items.TryGetValue(id, out var item);
        return await Task.FromResult(item);
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_items.Values);
    }

    public async Task SaveAsync(T aggregate, CancellationToken cancellationToken = default)
    {
        _items[aggregate.Id] = aggregate;
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _items.Remove(id);
        await Task.CompletedTask;
    }
}

public class InMemoryOrderEventStore : IEventStore
{
    private readonly List<InMemoryEventRecord> _events = [];
    private readonly Dictionary<string, int> _streamVersions = [];

    public async Task AppendAsync(
        string streamId,
        IEnumerable<IDomainEvent> events,
        int expectedVersion,
        string correlationId,
        string? causationId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));

        var eventsList = events.ToList();
        if (eventsList.Count == 0)
            return;

        _streamVersions.TryGetValue(streamId, out var currentVersion);
        if (currentVersion != expectedVersion)
        {
            throw new ConcurrencyException(
                $"Concurrency conflict: expected version {expectedVersion}, but current version is {currentVersion}");
        }

        var nextVersion = currentVersion;
        foreach (var evt in eventsList)
        {
            nextVersion++;
            _events.Add(new InMemoryEventRecord
            {
                StreamId = streamId,
                Version = nextVersion,
                DomainEvent = evt,
                CorrelationId = correlationId,
                CausationId = causationId,
                TenantId = tenantId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        _streamVersions[streamId] = nextVersion;
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<IDomainEvent>> LoadStreamAsync(string streamId, CancellationToken cancellationToken = default)
    {
        var events = _events
            .Where(e => e.StreamId == streamId)
            .OrderBy(e => e.Version)
            .Select(e => e.DomainEvent)
            .ToList();

        return await Task.FromResult(events.AsEnumerable());
    }

    public async Task<IEnumerable<IDomainEvent>> LoadStreamFromVersionAsync(
        string streamId,
        int fromVersion,
        CancellationToken cancellationToken = default)
    {
        var events = _events
            .Where(e => e.StreamId == streamId && e.Version > fromVersion)
            .OrderBy(e => e.Version)
            .Select(e => e.DomainEvent)
            .ToList();

        return await Task.FromResult(events.AsEnumerable());
    }
}

public class InMemoryEventRecord
{
    public string StreamId { get; set; } = string.Empty;
    public int Version { get; set; }
    public IDomainEvent DomainEvent { get; set; } = null!;
    public string CorrelationId { get; set; } = string.Empty;
    public string? CausationId { get; set; }
    public string? TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

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
}

public class InMemoryReadModelStore<T> : IReadModelStore<T> where T : class
{
    private readonly Dictionary<string, T> _items = [];

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _items.TryGetValue(id, out var item);
        return await Task.FromResult(item);
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_items.Values);
    }

    public async Task UpsertAsync(string id, T model, CancellationToken cancellationToken = default)
    {
        _items[id] = model;
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _items.Remove(id);
        await Task.CompletedTask;
    }
}
