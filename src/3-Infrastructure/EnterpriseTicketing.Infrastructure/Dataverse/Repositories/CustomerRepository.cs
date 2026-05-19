using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Infrastructure.Dataverse.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private const string EntityName = "new_customer";
    private static readonly string[] AllColumns =
        ["new_customerid", "new_fullname", "new_email", "new_phonenumber",
         "new_companyname", "new_accountnumber", "new_isactive", "createdon"];

    private readonly IDataverseService _dataverseService;
    private readonly ILogger<CustomerRepository> _logger;

    public CustomerRepository(IDataverseService dataverseService, ILogger<CustomerRepository> logger)
    {
        _dataverseService = dataverseService;
        _logger = logger;
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var attrs = await _dataverseService.GetEntityAsync(EntityName, id, AllColumns, cancellationToken);
        return attrs is not null ? MapToCustomer(attrs) : null;
    }

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var fetchXml = $"""
            <fetch top="1">
              <entity name="{EntityName}">
                {string.Join("\n", AllColumns.Select(c => $"<attribute name=\"{c}\" />"))}
                <filter>
                  <condition attribute="new_email" operator="eq" value="{email.ToLower()}" />
                </filter>
              </entity>
            </fetch>
            """;

        var (records, _) = await _dataverseService.QueryEntitiesAsync(EntityName, fetchXml, cancellationToken);
        return records.FirstOrDefault() is { } a ? MapToCustomer(a) : null;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dataverseService.ExistsAsync(EntityName, id, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        var attrs = new Dictionary<string, object>
        {
            ["new_customerid"] = customer.Id,
            ["new_fullname"] = customer.FullName,
            ["new_email"] = customer.Email.Value,
            ["new_isactive"] = customer.IsActive
        };

        if (customer.PhoneNumber is not null) attrs["new_phonenumber"] = customer.PhoneNumber;
        if (customer.CompanyName is not null) attrs["new_companyname"] = customer.CompanyName;

        await _dataverseService.CreateEntityAsync(EntityName, attrs, cancellationToken);
    }

    private static Customer MapToCustomer(Dictionary<string, object> attrs)
    {
        var id = attrs.TryGetValue("new_customerid", out var v) && v is Guid g ? g : Guid.Empty;
        var fullName = attrs.TryGetValue("new_fullname", out var fn) ? fn?.ToString() ?? "" : "";
        var email = attrs.TryGetValue("new_email", out var em) ? em?.ToString() ?? "" : "";
        var phone = attrs.TryGetValue("new_phonenumber", out var ph) ? ph?.ToString() : null;
        var company = attrs.TryGetValue("new_companyname", out var co) ? co?.ToString() : null;
        var account = attrs.TryGetValue("new_accountnumber", out var ac) ? ac?.ToString() : null;
        var isActive = attrs.TryGetValue("new_isactive", out var ia) && ia is bool b ? b : true;
        var createdAt = attrs.TryGetValue("createdon", out var cr) && cr is DateTime dt
            ? new DateTimeOffset(dt, TimeSpan.Zero)
            : DateTimeOffset.MinValue;

        return Customer.Reconstitute(id, fullName, email, phone, company, account, isActive, createdAt);
    }
}
