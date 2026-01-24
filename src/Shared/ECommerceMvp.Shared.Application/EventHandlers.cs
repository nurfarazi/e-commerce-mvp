using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.Shared.Application;

/// <summary>
/// Base interface for event handlers.
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
