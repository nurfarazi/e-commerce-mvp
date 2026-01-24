namespace ECommerceMvp.Shared.Application;

/// <summary>
/// Base interface for all queries.
/// </summary>
public interface IQuery<TResponse>
{
    // Marker interface for query type safety
}

/// <summary>
/// Base interface for query handlers.
/// </summary>
public interface IQueryHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base interface for query services.
/// </summary>
public interface IQueryService
{
    Task<TResult?> QueryAsync<TResult>(string collection, object? filter = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<TResult>> QueryManyAsync<TResult>(string collection, object? filter = null, CancellationToken cancellationToken = default);
}
/// <summary>
/// Query bus for sending queries to handlers.
/// </summary>
public interface IQueryBus
{
    Task<TResponse> SendAsync<TQuery, TResponse>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResponse>;
}

/// <summary>
/// Query bus implementation using dependency injection.
/// Routes queries to registered handlers.
/// </summary>
public class QueryBus : IQueryBus
{
    private readonly IServiceProvider _serviceProvider;

    public QueryBus(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task<TResponse> SendAsync<TQuery, TResponse>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResponse>
    {
        var handler = _serviceProvider.GetService(typeof(IQueryHandler<TQuery, TResponse>));

        if (handler == null)
            throw new InvalidOperationException(
                $"No handler registered for query type {typeof(TQuery).Name}");

        var queryHandler = (IQueryHandler<TQuery, TResponse>)handler;
        return await queryHandler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
    }
}