using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Events;
using EnterpriseTicketing.Domain.Exceptions;
using EnterpriseTicketing.Domain.ValueObjects;

namespace EnterpriseTicketing.Domain.Entities;

/// <summary>
/// Core aggregate root for the Ticket bounded context.
///
/// Enterprise design decisions:
/// 1. Private constructor + factory method (Create) enforces all invariants at creation time.
///    You cannot create an invalid Ticket — the compiler and domain prevent it.
/// 2. Domain events are collected on the entity (not dispatched inline).
///    This avoids the dual-write problem: events are dispatched after successful persistence.
/// 3. Status transition logic lives here, not in application handlers or controllers.
///    The Ticket entity is the authority on what state transitions are valid.
/// 4. All mutations are through explicit intent-revealing methods (Escalate, Close, Resolve).
///    No public setters — prevents accidental state corruption from calling code.
/// </summary>
public sealed class Ticket
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public TicketNumber TicketNumber { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketCategory Category { get; private set; }
    public Guid CustomerId { get; private set; }
    public string? AssignedToUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public int EscalationCount { get; private set; }
    public string? ResolutionNotes { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // Required for ORM / Dataverse mapping reconstruction
    private Ticket() { }

    /// <summary>
    /// Factory method for creating new tickets. Validates all invariants before raising the domain event.
    /// This is the single entry point for ticket creation — ensuring consistent initialization.
    /// </summary>
    public static Ticket Create(
        TicketNumber ticketNumber,
        string title,
        string description,
        TicketPriority priority,
        TicketCategory category,
        Guid customerId,
        string? assignedToUserId = null)
    {
        ArgumentNullException.ThrowIfNull(ticketNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (title.Length > 200)
            throw new DomainException("Title cannot exceed 200 characters.", "TITLE_TOO_LONG");

        if (description.Length > 4000)
            throw new DomainException("Description cannot exceed 4000 characters.", "DESCRIPTION_TOO_LONG");

        if (customerId == Guid.Empty)
            throw new DomainException("A valid customer must be provided.", "INVALID_CUSTOMER_ID");

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            TicketNumber = ticketNumber,
            Title = title.Trim(),
            Description = description.Trim(),
            Status = TicketStatus.Open,
            Priority = priority,
            Category = category,
            CustomerId = customerId,
            AssignedToUserId = assignedToUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            EscalationCount = 0
        };

        ticket._domainEvents.Add(new TicketCreatedEvent
        {
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber.Value,
            CustomerId = ticket.CustomerId,
            Priority = ticket.Priority,
            Category = ticket.Category,
            Title = ticket.Title
        });

        return ticket;
    }

    /// <summary>
    /// Reconstructs a Ticket entity from persisted data (e.g., from Dataverse).
    /// Does NOT raise domain events — this represents an existing state, not a new action.
    /// </summary>
    public static Ticket Reconstitute(
        Guid id,
        TicketNumber ticketNumber,
        string title,
        string description,
        TicketStatus status,
        TicketPriority priority,
        TicketCategory category,
        Guid customerId,
        string? assignedToUserId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? resolvedAt,
        DateTimeOffset? closedAt,
        int escalationCount,
        string? resolutionNotes)
    {
        return new Ticket
        {
            Id = id,
            TicketNumber = ticketNumber,
            Title = title,
            Description = description,
            Status = status,
            Priority = priority,
            Category = category,
            CustomerId = customerId,
            AssignedToUserId = assignedToUserId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ResolvedAt = resolvedAt,
            ClosedAt = closedAt,
            EscalationCount = escalationCount,
            ResolutionNotes = resolutionNotes
        };
    }

    public void UpdateDetails(string title, string description, TicketPriority priority, TicketCategory category)
    {
        if (Status is TicketStatus.Closed or TicketStatus.Cancelled)
            throw new InvalidTicketStateException(Id, Status, nameof(UpdateDetails));

        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
        Category = category;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Assign(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (Status is TicketStatus.Closed or TicketStatus.Cancelled)
            throw new InvalidTicketStateException(Id, Status, nameof(Assign));

        AssignedToUserId = userId;
        UpdatedAt = DateTimeOffset.UtcNow;

        if (Status == TicketStatus.Open)
        {
            var oldStatus = Status;
            Status = TicketStatus.InProgress;
            _domainEvents.Add(new TicketStatusChangedEvent
            {
                TicketId = Id,
                TicketNumber = TicketNumber.Value,
                OldStatus = oldStatus,
                NewStatus = Status,
                ChangedByUserId = userId
            });
        }
    }

    public void ChangeStatus(TicketStatus newStatus, string changedByUserId)
    {
        ValidateStatusTransition(Status, newStatus);

        var oldStatus = Status;
        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new TicketStatusChangedEvent
        {
            TicketId = Id,
            TicketNumber = TicketNumber.Value,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByUserId = changedByUserId
        });
    }

    public void Resolve(string resolutionNotes, string resolvedByUserId)
    {
        if (Status is TicketStatus.Closed or TicketStatus.Cancelled or TicketStatus.Resolved)
            throw new InvalidTicketStateException(Id, Status, nameof(Resolve));

        ArgumentException.ThrowIfNullOrWhiteSpace(resolutionNotes);

        var oldStatus = Status;
        Status = TicketStatus.Resolved;
        ResolutionNotes = resolutionNotes;
        ResolvedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new TicketStatusChangedEvent
        {
            TicketId = Id,
            TicketNumber = TicketNumber.Value,
            OldStatus = oldStatus,
            NewStatus = TicketStatus.Resolved,
            ChangedByUserId = resolvedByUserId
        });
    }

    public void Close(string closedByUserId)
    {
        if (Status == TicketStatus.Closed)
            throw new InvalidTicketStateException(Id, Status, nameof(Close));

        if (Status == TicketStatus.Cancelled)
            throw new InvalidTicketStateException(Id, Status, nameof(Close));

        var oldStatus = Status;
        Status = TicketStatus.Closed;
        ClosedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new TicketStatusChangedEvent
        {
            TicketId = Id,
            TicketNumber = TicketNumber.Value,
            OldStatus = oldStatus,
            NewStatus = TicketStatus.Closed,
            ChangedByUserId = closedByUserId
        });
    }

    public void Escalate(string reason, string escalatedByUserId)
    {
        if (Status is TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled)
            throw new InvalidTicketStateException(Id, Status, nameof(Escalate));

        EscalationCount++;
        UpdatedAt = DateTimeOffset.UtcNow;

        // Auto-upgrade priority on repeated escalations
        if (EscalationCount >= 3 && Priority < TicketPriority.Critical)
            Priority = TicketPriority.Critical;

        _domainEvents.Add(new TicketEscalatedEvent
        {
            TicketId = Id,
            TicketNumber = TicketNumber.Value,
            EscalationLevel = EscalationCount,
            Reason = reason,
            EscalatedByUserId = escalatedByUserId
        });
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private static void ValidateStatusTransition(TicketStatus current, TicketStatus next)
    {
        // Define allowed transitions explicitly — prevents illegal state machine moves
        var allowedTransitions = new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.Open] = [TicketStatus.InProgress, TicketStatus.PendingCustomer, TicketStatus.Cancelled],
            [TicketStatus.InProgress] = [TicketStatus.PendingCustomer, TicketStatus.PendingThirdParty, TicketStatus.Resolved, TicketStatus.Cancelled],
            [TicketStatus.PendingCustomer] = [TicketStatus.InProgress, TicketStatus.Closed, TicketStatus.Cancelled],
            [TicketStatus.PendingThirdParty] = [TicketStatus.InProgress, TicketStatus.Cancelled],
            [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
            [TicketStatus.Closed] = [],
            [TicketStatus.Cancelled] = []
        };

        if (!allowedTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(next))
            throw new DomainException(
                $"Cannot transition ticket from '{current}' to '{next}'.",
                "INVALID_STATUS_TRANSITION");
    }
}
