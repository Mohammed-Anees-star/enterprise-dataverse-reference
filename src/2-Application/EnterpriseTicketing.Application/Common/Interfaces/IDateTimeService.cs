namespace EnterpriseTicketing.Application.Common.Interfaces;

/// <summary>
/// Abstracts DateTimeOffset.UtcNow to enable deterministic testing.
/// Never use DateTime.Now or DateTimeOffset.UtcNow directly in application handlers.
/// </summary>
public interface IDateTimeService
{
    DateTimeOffset UtcNow { get; }
}
