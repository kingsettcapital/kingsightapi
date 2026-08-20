using System.Text.Json.Serialization;

namespace kingsightapi.Entities;

/// <summary>Single value for a list-page filter dropdown (value sent back as query param).</summary>
public sealed class PortalFilterOptionDto
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

/// <summary>Quarter option for Investors/Investments list pages when view is quarterly.</summary>
public sealed class PortalQuarterPeriodOptionDto
{
    [JsonPropertyName("date_key")]
    public int DateKey { get; init; }

    [JsonPropertyName("calendar_year")]
    public int CalendarYear { get; init; }

    public int Quarter { get; init; }

    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;
}

/// <summary>Filter dropdown values for GET /api/CapitalInvestors/filter-options.</summary>
public sealed class InvestorListFilterOptionsDto
{
    [JsonPropertyName("investor_types")]
    public IReadOnlyList<PortalFilterOptionDto> InvestorTypes { get; init; } = [];

    public IReadOnlyList<PortalFilterOptionDto> Relationships { get; init; } = [];

    [JsonPropertyName("calendar_years")]
    public IReadOnlyList<PortalFilterOptionDto> CalendarYears { get; init; } = [];

    [JsonPropertyName("quarterly_periods")]
    public IReadOnlyList<PortalQuarterPeriodOptionDto> QuarterlyPeriods { get; init; } = [];
}

/// <summary>Filter dropdown values for GET /api/Funds/filter-options.</summary>
public sealed class FundListFilterOptionsDto
{
    [JsonPropertyName("fund_types")]
    public IReadOnlyList<PortalFilterOptionDto> FundTypes { get; init; } = [];

    public IReadOnlyList<PortalFilterOptionDto> Strategies { get; init; } = [];

    [JsonPropertyName("calendar_years")]
    public IReadOnlyList<PortalFilterOptionDto> CalendarYears { get; init; } = [];

    [JsonPropertyName("quarterly_periods")]
    public IReadOnlyList<PortalQuarterPeriodOptionDto> QuarterlyPeriods { get; init; } = [];
}

/// <summary>Filter dropdown values for GET /api/Assets/filter-options.</summary>
public sealed class AssetListFilterOptionsDto
{
    [JsonPropertyName("asset_types")]
    public IReadOnlyList<PortalFilterOptionDto> AssetTypes { get; init; } = [];

    [JsonPropertyName("investment_types")]
    public IReadOnlyList<PortalFilterOptionDto> InvestmentTypes { get; init; } = [];

    public IReadOnlyList<PortalFilterOptionDto> Geographies { get; init; } = [];

    public IReadOnlyList<PortalFilterOptionDto> Statuses { get; init; } = [];

    [JsonPropertyName("quarterly_periods")]
    public IReadOnlyList<PortalQuarterPeriodOptionDto> QuarterlyPeriods { get; init; } = [];
}
