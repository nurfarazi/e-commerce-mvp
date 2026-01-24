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
        var builder = WebApplicationBuilder.CreateBuilder(args);

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
public class InMemoryOrderRepository<T> : IRepository<T, string> where T : IAggregateRoot
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
    private readonly List<DomainEventEnvelope> _events = [];

    public async Task AppendAsync(DomainEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        _events.Add(envelope);
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<DomainEventEnvelope>> GetEventsByAggregateIdAsync(string aggregateId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_events.Where(e => e.DomainEvent.AggregateId == aggregateId));
    }

    public async Task<IEnumerable<DomainEventEnvelope>> GetAllEventsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_events);
    }

    public async Task<IEnumerable<DomainEventEnvelope>> GetEventsSinceAsync(long sequenceNumber, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_events.Skip((int)sequenceNumber));
    }
}

public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, string> _processed = [];

    public async Task<IdempotencyCheckResult> CheckIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (_processed.TryGetValue(idempotencyKey, out var aggregateId))
        {
            return new IdempotencyCheckResult { IsIdempotent = true, AggregateId = aggregateId };
        }
        return new IdempotencyCheckResult { IsIdempotent = false };
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
