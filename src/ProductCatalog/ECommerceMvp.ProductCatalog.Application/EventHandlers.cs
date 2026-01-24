using ECommerceMvp.ProductCatalog.Domain;
using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.ProductCatalog.Application;

/// <summary>
/// Event handlers for domain events to project to read models (CQRS projections).
/// These handlers update the read model databases based on domain events.
/// </summary>
public interface IProductProjectionWriter
{
    /// <summary>
    /// Handles ProductCreatedEvent and projects to ProductListView and ProductDetailView.
    /// </summary>
    Task HandleProductCreatedAsync(ProductCreatedEvent @event, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles ProductDetailsUpdatedEvent and updates ProductDetailView projection.
    /// </summary>
    Task HandleProductDetailsUpdatedAsync(ProductDetailsUpdatedEvent @event, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles ProductPriceChangedEvent and updates both ProductListView and ProductDetailView projections.
    /// </summary>
    Task HandleProductPriceChangedAsync(ProductPriceChangedEvent @event, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles ProductActivatedEvent and updates IsActive flag in both projections.
    /// </summary>
    Task HandleProductActivatedAsync(ProductActivatedEvent @event, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles ProductDeactivatedEvent and updates IsActive flag in both projections.
    /// </summary>
    Task HandleProductDeactivatedAsync(ProductDeactivatedEvent @event, string correlationId, CancellationToken cancellationToken = default);
}
