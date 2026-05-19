namespace EnterpriseTicketing.Domain.Interfaces;

/// <summary>
/// Unit of Work abstraction. With Dataverse there is no real transactional boundary
/// at the application level - the SDK uses optimistic concurrency via ETags and
/// multi-step writes are coordinated via ExecuteTransactionRequest under the hood.
///
/// We still surface this interface so that:
///   * Handlers can express atomicity intent
///   * Domain event dispatch is deferred until after persistence succeeds
///   * Future migration to a relational store does not break handler code
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task DispatchDomainEventsAsync(CancellationToken cancellationToken = default);
}
