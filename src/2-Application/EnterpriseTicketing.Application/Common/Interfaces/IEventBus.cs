using EnterpriseTicketing.Domain.Events;

namespace EnterpriseTicketing.Application.Common.Interfaces;

/// <summary>
/// Abstraction for the event bus (implemented by Azure Service Bus in Infrastructure).
/// Publishing domain events through this abstraction decouples the application layer
/// from the messaging infrastructure, enabling easy testing with in-memory implementations.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IDomainEvent;
}
