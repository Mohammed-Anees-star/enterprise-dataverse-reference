using EnterpriseTicketing.Domain.Entities;

namespace EnterpriseTicketing.Infrastructure.Dataverse.Mapping;

public static class CustomerMapper
{
    public const string EntityLogicalName = "new_customer";
    public const string EntitySetName = "new_customers";

    public const string IdColumn = "new_customerid";
    public const string FullNameColumn = "new_fullname";
    public const string EmailColumn = "new_email";
    public const string PhoneColumn = "new_phonenumber";
    public const string CompanyColumn = "new_companyname";
    public const string AccountNumberColumn = "new_accountnumber";
    public const string IsActiveColumn = "new_isactive";
    public const string CreatedAtColumn = "createdon";

    public static IReadOnlyList<string> AllColumns =>
    [
        IdColumn, FullNameColumn, EmailColumn, PhoneColumn, CompanyColumn,
        AccountNumberColumn, IsActiveColumn, CreatedAtColumn
    ];

    public static Dictionary<string, object> ToDataverseAttributes(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var attributes = new Dictionary<string, object>
        {
            [FullNameColumn] = customer.FullName,
            [EmailColumn] = customer.Email.Value,
            [IsActiveColumn] = customer.IsActive
        };

        if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
            attributes[PhoneColumn] = customer.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(customer.CompanyName))
            attributes[CompanyColumn] = customer.CompanyName;

        if (!string.IsNullOrWhiteSpace(customer.AccountNumber))
            attributes[AccountNumberColumn] = customer.AccountNumber;

        return attributes;
    }

    public static Customer FromDataverseAttributes(IReadOnlyDictionary<string, object?> attributes)
    {
        var id = (attributes.TryGetValue(IdColumn, out var idVal) && idVal is Guid g) ? g : Guid.Empty;
        var createdAt = attributes.TryGetValue(CreatedAtColumn, out var ca) && ca is DateTime dt
            ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc))
            : DateTimeOffset.UtcNow;

        return Customer.Reconstitute(
            id: id,
            fullName: attributes[FullNameColumn] as string ?? string.Empty,
            email: attributes[EmailColumn] as string ?? string.Empty,
            phoneNumber: attributes.GetValueOrDefault(PhoneColumn) as string,
            companyName: attributes.GetValueOrDefault(CompanyColumn) as string,
            accountNumber: attributes.GetValueOrDefault(AccountNumberColumn) as string,
            isActive: attributes.GetValueOrDefault(IsActiveColumn) is bool b && b,
            createdAt: createdAt);
    }
}
