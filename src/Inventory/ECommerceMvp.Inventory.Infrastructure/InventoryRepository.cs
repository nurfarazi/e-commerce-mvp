using ECommerceMvp.Inventory.Domain;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.Inventory.Infrastructure;

/// <summary>
/// InventoryItem repository implementation using event sourcing.
/// </summary>
public class InventoryRepository : IRepository<InventoryItem, string>
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<InventoryRepository> _logger;

    public InventoryRepository(IEventStore eventStore, ILogger<InventoryRepository> logger)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InventoryItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading inventory item for product {ProductId}", id);

        var events = await _eventStore.LoadStreamAsync($"inventory-{id}", cancellationToken).ConfigureAwait(false);
        var eventsList = events.ToList();

        if (eventsList.Count == 0)
            return null;

        var item = InventoryItem.FromHistory(id);
        item.LoadFromHistory(eventsList);

        _logger.LogDebug("Loaded inventory item for product {ProductId} with version {Version}", id, item.Version);
        return item;
    }

    public async Task SaveAsync(InventoryItem aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate == null)
            throw new ArgumentNullException(nameof(aggregate));

        var uncommittedEvents = aggregate.UncommittedEvents.ToList();
        if (uncommittedEvents.Count == 0)
            return;

        var streamId = $"inventory-{aggregate.Id}";
        var expectedVersion = aggregate.Version - uncommittedEvents.Count;

        _logger.LogDebug(
            "Saving inventory item for product {ProductId}: {EventCount} events, expected version {ExpectedVersion}",
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
            _logger.LogDebug("Inventory item for product {ProductId} saved successfully, new version {Version}",
                aggregate.Id, aggregate.Version);
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict saving inventory item for product {ProductId}", aggregate.Id);
            throw;
        }
    }
}
