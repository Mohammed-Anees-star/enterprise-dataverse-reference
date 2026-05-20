namespace EnterpriseTicketing.Application.Common.Interfaces;

/// <summary>
/// Generates the next unique TicketNumber sequence value.
///
/// Two production implementations are provided (choose one per deployment):
///
/// Option A — Dataverse Auto-Number column (recommended for Power Platform-native deployments):
///   Configure the <c>new_ticketnumber</c> column as an AutoNumber column in Dataverse.
///   Set format: TKT-{YYYY}-{SEQNUM:6}.
///   The SDK returns the auto-generated value from the Create response; this interface
///   acts as a thin no-op wrapper that reads the value back after create.
///
/// Option B — Dataverse sequence record (used when auto-number format control is needed):
///   A dedicated <c>new_ticketsequence</c> record in Dataverse holds the current counter.
///   Increment is performed with an optimistic lock (ETag) to prevent race conditions.
///   Falls back to a Polly retry on 409 Conflict.
///
/// The interface is injected into <see cref="CreateTicketCommandHandler"/> so the
/// strategy can be swapped without touching the handler.
/// </summary>
public interface ITicketNumberSequence
{
    /// <summary>
    /// Returns the next formatted sequence integer for the given calendar year.
    /// Implementations MUST be safe to call concurrently from multiple host instances.
    /// </summary>
    Task<int> NextAsync(int year, CancellationToken cancellationToken = default);
}
