using System.Globalization;
using System.Text;

namespace EnterpriseTicketing.Infrastructure.Dataverse;

/// <summary>
/// Fluent builder for OData v4 query options against the Dataverse Web API.
/// Generates strings like <c>?$select=...&amp;$filter=...&amp;$top=20&amp;$skip=0&amp;$count=true</c>.
///
/// We deliberately keep this builder dumb - no validation of filter expression syntax,
/// no LINQ provider. OData v4 supports a vast surface area; layering a LINQ provider
/// on top adds significant complexity that this reference implementation doesn't need.
/// </summary>
public sealed class ODataQueryOptions
{
    public string? Select { get; private set; }
    public string? Filter { get; private set; }
    public string? OrderBy { get; private set; }
    public int? Top { get; private set; }
    public int? Skip { get; private set; }
    public string? Expand { get; private set; }
    public bool Count { get; private set; }

    public ODataQueryOptions WithSelect(params string[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        Select = string.Join(",", columns);
        return this;
    }

    public ODataQueryOptions WithFilter(string filter)
    {
        Filter = filter;
        return this;
    }

    public ODataQueryOptions WithOrderBy(string orderBy)
    {
        OrderBy = orderBy;
        return this;
    }

    public ODataQueryOptions WithTop(int top)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);
        Top = top;
        return this;
    }

    public ODataQueryOptions WithSkip(int skip)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        Skip = skip;
        return this;
    }

    public ODataQueryOptions WithExpand(string expand)
    {
        Expand = expand;
        return this;
    }

    public ODataQueryOptions WithCount(bool count = true)
    {
        Count = count;
        return this;
    }

    /// <summary>
    /// Build a leading-? query string. Returns an empty string if no options are set.
    /// </summary>
    public string BuildQueryString()
    {
        var sb = new StringBuilder();
        AppendParam(sb, "$select", Select);
        AppendParam(sb, "$filter", Filter);
        AppendParam(sb, "$orderby", OrderBy);
        if (Top.HasValue) AppendParam(sb, "$top", Top.Value.ToString(CultureInfo.InvariantCulture));
        if (Skip.HasValue) AppendParam(sb, "$skip", Skip.Value.ToString(CultureInfo.InvariantCulture));
        AppendParam(sb, "$expand", Expand);
        if (Count) AppendParam(sb, "$count", "true");

        if (sb.Length == 0) return string.Empty;
        sb.Insert(0, '?');
        return sb.ToString();
    }

    public override string ToString() => BuildQueryString();

    private static void AppendParam(StringBuilder sb, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (sb.Length > 0) sb.Append('&');
        sb.Append(name).Append('=').Append(Uri.EscapeDataString(value));
    }
}

/// <summary>
/// Generic wrapper around the OData response envelope:
/// <c>{ "@odata.context": "...", "@odata.count": 42, "value": [ ... ] }</c>.
/// </summary>
public sealed class ODataCollection<T>
{
    public string? Context { get; init; }
    public int? Count { get; init; }
    public string? NextLink { get; init; }
    public required IReadOnlyList<T> Value { get; init; }
}
