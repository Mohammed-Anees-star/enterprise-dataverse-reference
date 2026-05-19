using System.Text.RegularExpressions;

namespace EnterpriseTicketing.Domain.ValueObjects;

/// <summary>
/// Value object for email addresses. Enforces format validation at the domain boundary.
/// Using a value object instead of a raw string makes the domain intent clear and
/// prevents invalid email addresses from ever entering the system.
/// </summary>
public sealed partial class EmailAddress : IEquatable<EmailAddress>
{
    private static readonly Regex EmailRegex = GenerateEmailRegex();

    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value.ToLowerInvariant();
    }

    public static EmailAddress Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim();
        if (!EmailRegex.IsMatch(trimmed))
            throw new ArgumentException($"'{value}' is not a valid email address.", nameof(value));

        return new EmailAddress(trimmed);
    }

    public static bool TryCreate(string? value, out EmailAddress? emailAddress)
    {
        emailAddress = null;
        if (string.IsNullOrWhiteSpace(value)) return false;

        try
        {
            emailAddress = Create(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Equals(EmailAddress? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is EmailAddress other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.OrdinalIgnoreCase);
    public override string ToString() => Value;

    public static implicit operator string(EmailAddress email) => email.Value;

    [GeneratedRegex(@"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GenerateEmailRegex();
}
