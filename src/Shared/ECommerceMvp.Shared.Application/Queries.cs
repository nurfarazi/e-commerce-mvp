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
