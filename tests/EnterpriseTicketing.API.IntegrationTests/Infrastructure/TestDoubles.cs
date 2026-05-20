using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Events;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace EnterpriseTicketing.API.IntegrationTests.Infrastructure;

// ─────────────────────────────────────────────────────────────────────────────
// In-memory ITicketRepository
// ─────────────────────────────────────────────────────────────────────────────

public sealed class InMemoryTicketStore : ITicketRepository
{
    private readonly ConcurrentDictionary<Guid, Ticket> _store = new();

    public void Seed(Ticket ticket) => _store[ticket.Id] = ticket;
    public IReadOnlyList<Ticket> All => _store.Values.ToList();

    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var t) ? t : null);

    public Task<Ticket?> GetByTicketNumberAsync(string n, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(t => t.TicketNumber.Value == n));

    public Task<(IReadOnlyList<Ticket> Items, int TotalCount)> GetPagedAsync(
        TicketFilter filter, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var items = _store.Values
            .Where(t => filter.Status is null || t.Status == filter.Status)
            .Where(t => filter.Priority is null || t.Priority == filter.Priority)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        var total = items.Count;
        var page  = items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(((IReadOnlyList<Ticket>)page, total));
    }

    public Task<IReadOnlyList<Ticket>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Ticket>>(
            _store.Values.Where(t => t.CustomerId == customerId).ToList());

    public Task AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        _store[ticket.Id] = ticket;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        _store[ticket.Id] = ticket;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.ContainsKey(id));
}

// ─────────────────────────────────────────────────────────────────────────────
// In-memory ICustomerRepository
// ─────────────────────────────────────────────────────────────────────────────

public sealed class InMemoryCustomerStore : ICustomerRepository
{
    private readonly ConcurrentDictionary<Guid, Customer> _store = new();

    public void Seed(Customer customer) => _store[customer.Id] = customer;

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var c) ? c : null);

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_store.Values.FirstOrDefault(c => c.Email.Value == email.ToLower()));

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.ContainsKey(id));

    public Task AddAsync(Customer customer, CancellationToken ct = default)
    {
        _store[customer.Id] = customer;
        return Task.CompletedTask;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Deterministic ITicketNumberSequence
// ─────────────────────────────────────────────────────────────────────────────

public sealed class InMemorySequence : ITicketNumberSequence
{
    private int _counter;

    public Task<int> NextAsync(int year, CancellationToken ct = default) =>
        Task.FromResult(Interlocked.Increment(ref _counter));
}

// ─────────────────────────────────────────────────────────────────────────────
// Capturing IOutboxStore
// ─────────────────────────────────────────────────────────────────────────────

public sealed class InMemoryOutboxStore : IOutboxStore
{
    private readonly List<IDomainEvent> _events = [];

    public IReadOnlyList<IDomainEvent> CapturedEvents => _events.AsReadOnly();

    public Task AppendAsync(IDomainEvent @event, CancellationToken ct = default)
    {
        _events.Add(@event);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxEntry>> GetUnpublishedAsync(int batchSize = 50, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OutboxEntry>>([]);

    public Task MarkPublishedAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    public Task IncrementRetryCountAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
}

// ─────────────────────────────────────────────────────────────────────────────
// ICurrentUserService test implementation
// ─────────────────────────────────────────────────────────────────────────────

public sealed class TestCurrentUserService : ICurrentUserService
{
    public TestCurrentUserService(string userId, string userName)
    {
        UserId = userId;
        UserName = userName;
    }

    public string? UserId { get; }
    public string? UserName { get; }
    public string? Email => "test@example.com";
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => ["Ticket.Reader", "Ticket.Writer", "Ticket.Manager", "Ticket.Admin"];
    public bool IsInRole(string role) => true;
}
