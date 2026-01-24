using ECommerceMvp.ProductCatalog.Domain;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.ProductCatalog.Infrastructure;

/// <summary>
/// Product repository implementation using event sourcing.
/// </summary>
public class ProductRepository : IRepository<Product, string>
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<ProductRepository> _logger;

    public ProductRepository(IEventStore eventStore, ILogger<ProductRepository> logger)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Product?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading product {ProductId}", id);

        var events = await _eventStore.LoadStreamAsync($"product-{id}", cancellationToken).ConfigureAwait(false);
        var eventsList = events.ToList();

        if (eventsList.Count == 0)
            return null;

        var product = Product.FromHistory(id);
        product.LoadFromHistory(eventsList);

        _logger.LogDebug("Loaded product {ProductId} with version {Version}", id, product.Version);
        return product;
    }

    public async Task SaveAsync(Product aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate == null)
            throw new ArgumentNullException(nameof(aggregate));

        var uncommittedEvents = aggregate.UncommittedEvents.ToList();
        if (uncommittedEvents.Count == 0)
            return;

        var streamId = $"product-{aggregate.Id}";
        var expectedVersion = aggregate.Version - uncommittedEvents.Count;

        _logger.LogDebug(
            "Saving product {ProductId}: {EventCount} events, expected version {ExpectedVersion}",
            aggregate.Id, uncommittedEvents.Count, expectedVersion);

        try
        {
            await _eventStore.AppendAsync(
                streamId,
                uncommittedEvents,
                expectedVersion,
                Guid.NewGuid().ToString(), // CorrelationId
                null,
                null,
                cancellationToken).ConfigureAwait(false);

            aggregate.ClearUncommittedEvents();
            _logger.LogDebug("Product {ProductId} saved successfully, new version {Version}",
                aggregate.Id, aggregate.Version);
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict saving product {ProductId}", aggregate.Id);
            throw;
        }
    }
}
