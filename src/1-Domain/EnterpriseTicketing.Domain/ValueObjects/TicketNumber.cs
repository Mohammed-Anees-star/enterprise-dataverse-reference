namespace EnterpriseTicketing.Domain.ValueObjects;

/// <summary>
/// Value object representing a unique, human-readable ticket identifier.
/// Format: TKT-{YYYY}-{000000} e.g. TKT-2025-000042
///
/// Value objects are immutable and equality is based on their content, not identity.
/// This prevents accidental reassignment and makes domain intent explicit.
/// </summary>
public sealed class TicketNumber : IEquatable<TicketNumber>
{
    private const string Prefix = "TKT";

    public string Value { get; }

    private TicketNumber(string value)
    {
        Value = value;
    }

    public static TicketNumber Create(int year, int sequence)
    {
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence must be positive.");

        return new TicketNumber($"{Prefix}-{year:D4}-{sequence:D6}");
    }

    public static TicketNumber Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!IsValid(value))
            throw new ArgumentException($"Invalid ticket number format: '{value}'. Expected format: TKT-YYYY-NNNNNN", nameof(value));

        return new TicketNumber(value);
    }

    public static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('-');
        return parts.Length == 3
            && parts[0] == Prefix
            && int.TryParse(parts[1], out var year) && year >= 2020
            && int.TryParse(parts[2], out var seq) && seq > 0;
    }

    public bool Equals(TicketNumber? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is TicketNumber other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;

    public static implicit operator string(TicketNumber ticketNumber) => ticketNumber.Value;
    public static explicit operator TicketNumber(string value) => Parse(value);
}
