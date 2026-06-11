namespace kingsightapi.Entities;

/// <summary>How section content is rendered in the Angular dashboard.</summary>
public enum DashboardSectionLayout
{
    /// <summary>Top KPI card row (always visible on module load).</summary>
    KpiRow,

    /// <summary>Flat key-value rows inside an accordion.</summary>
    Fields,

    /// <summary>Multiple titled groups of key-value rows (e.g. analytics breakdowns).</summary>
    GroupedFields,

    /// <summary>Paginated data grid; rows are fetched from <see cref="DashboardSectionDefinitionDto.DataRoute"/>.</summary>
    Table
}

/// <summary>When the frontend should fetch section payload.</summary>
public enum DashboardSectionLoadStrategy
{
    /// <summary>Fetch on module page load (KPI summary).</summary>
    Eager,

    /// <summary>Fetch when the user expands the accordion.</summary>
    Lazy
}

/// <summary>Section catalog row returned by GET /api/dashboard/modules/{module}/sections.</summary>
public sealed class DashboardSectionDefinitionDto
{
    public string Id { get; init; } = string.Empty;
    public string Module { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public string Layout { get; init; } = string.Empty;
    public string LoadStrategy { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Optional dedicated data route for table sections (e.g. /api/CapitalInvestors).
    /// Field-based sections use GET /api/dashboard/sections/{id} instead.
    /// </summary>
    public string? DataRoute { get; init; }
}

/// <summary>Lazy-loaded section payload returned by GET /api/dashboard/sections/{sectionId}.</summary>
public sealed class DashboardSectionDataDto
{
    public string SectionId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Layout { get; init; } = string.Empty;
    public string View { get; init; } = string.Empty;
    public IReadOnlyList<DashboardKpiCardDto>? Kpis { get; init; }
    public IReadOnlyList<DynamicFieldDto>? Fields { get; init; }
    public IReadOnlyList<DashboardSectionGroupDto>? Groups { get; init; }
}

/// <summary>Named group inside a grouped-fields section (e.g. "By Investor Type").</summary>
public sealed class DashboardSectionGroupDto
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<DynamicFieldDto> Fields { get; init; } = [];
}

/// <summary>KPI summary card at the top of a module dashboard.</summary>
public sealed class DashboardKpiCardDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public object? Value { get; init; }
    public string FormatType { get; init; } = FieldDataTypes.Text;
    public object? Change { get; init; }
    public string? ChangeFormatType { get; init; }
    public string? Caption { get; init; }
}

/// <summary>Cross-investor transaction row for the Investors module transactions table.</summary>
public sealed class DashboardTransactionDto
{
    public DateTime? Date { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string FundName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
