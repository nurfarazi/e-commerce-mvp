using ECommerceMvp.ProductCatalog.Domain;
using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.ProductCatalog.Application;

/// <summary>
/// Event handler for ProjectCreatedEvent: projects to read model.
/// </summary>
public interface IProductProjectionWriter
{
    Task HandleProductCreatedAsync(ProductCreatedEvent @event, string correlationId, CancellationToken cancellationToken = default);
    Task HandleProductUpdatedAsync(ProductUpdatedEvent @event, string correlationId, CancellationToken cancellationToken = default);
    Task HandleProductActivatedAsync(ProductActivatedEvent @event, string correlationId, CancellationToken cancellationToken = default);
    Task HandleProductDeactivatedAsync(ProductDeactivatedEvent @event, string correlationId, CancellationToken cancellationToken = default);
}
