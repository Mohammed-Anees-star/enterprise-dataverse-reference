using EnterpriseTicketing.Domain.ValueObjects;

namespace EnterpriseTicketing.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public EmailAddress Email { get; private set; } = null!;
    public string? PhoneNumber { get; private set; }
    public string? CompanyName { get; private set; }
    public string? AccountNumber { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Customer() { }

    public static Customer Create(string fullName, string email, string? phoneNumber = null, string? companyName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        return new Customer
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = EmailAddress.Create(email),
            PhoneNumber = phoneNumber?.Trim(),
            CompanyName = companyName?.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Customer Reconstitute(Guid id, string fullName, string email, string? phoneNumber,
        string? companyName, string? accountNumber, bool isActive, DateTimeOffset createdAt)
    {
        return new Customer
        {
            Id = id,
            FullName = fullName,
            Email = EmailAddress.Create(email),
            PhoneNumber = phoneNumber,
            CompanyName = companyName,
            AccountNumber = accountNumber,
            IsActive = isActive,
            CreatedAt = createdAt
        };
    }
}
