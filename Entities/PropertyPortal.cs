namespace kingsightapi.Entities;

using System.Text.Json.Serialization;

/// <summary>Property row on the module list page (Assets tab).</summary>
public sealed class PropertyListItemDto
{
    public long PropertyKey { get; init; }

    [JsonPropertyName("property_code")]
    public string PropertyCode { get; init; } = string.Empty;

    public string PropertyName { get; init; } = string.Empty;
    public string Geography { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Province { get; init; } = string.Empty;

    public string AssetType { get; init; } = string.Empty;

    [JsonPropertyName("investment_type")]
    public string InvestmentType { get; init; } = string.Empty;

    [JsonPropertyName("development_type")]
    public string DevelopmentType { get; init; } = string.Empty;

    [JsonPropertyName("property_status")]
    public string PropertyStatus { get; init; } = string.Empty;

    /// <summary>Gross leasable area (sf) from latest <c>fact_asset_metrics</c> row.</summary>
    [JsonPropertyName("gla_sf")]
    public decimal? GlaSf { get; init; }

    [JsonPropertyName("occupied_sf")]
    public decimal? OccupiedSf { get; init; }

    [JsonPropertyName("committed_sf")]
    public decimal? CommittedSf { get; init; }

    [JsonPropertyName("vacant_sf")]
    public decimal? VacantSf { get; init; }

    /// <summary>Legacy alias for <see cref="PropertyStatus"/>.</summary>
    public string Status => PropertyStatus;

    public bool IsPortfolio { get; init; }
}

/// <summary>Flat asset profile for GET /api/Assets/{propertyKey}.</summary>
public sealed class PropertyProfileDto
{
    [JsonPropertyName("property_key")]
    public long PropertyKey { get; init; }

    [JsonPropertyName("property_code")]
    public string PropertyCode { get; init; } = string.Empty;

    [JsonPropertyName("property_name")]
    public string PropertyName { get; init; } = string.Empty;

    public string Geography { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Province { get; init; } = string.Empty;

    [JsonPropertyName("asset_type")]
    public string AssetType { get; init; } = string.Empty;

    [JsonPropertyName("investment_type")]
    public string InvestmentType { get; init; } = string.Empty;

    [JsonPropertyName("development_type")]
    public string DevelopmentType { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("is_portfolio")]
    public bool IsPortfolio { get; init; }

    [JsonPropertyName("acquisition_date")]
    public DateTime? AcquisitionDate { get; init; }

    /// <summary>Gross leasable area (sf) from latest <c>fact_asset_metrics</c>.</summary>
    [JsonPropertyName("total_gla_sf")]
    public decimal? TotalGlaSf { get; init; }

    [JsonPropertyName("committed_area_sf")]
    public decimal? CommittedAreaSf { get; init; }

    [JsonPropertyName("vacant_area_sf")]
    public decimal? VacantAreaSf { get; init; }

    [JsonPropertyName("occupied_area_sf")]
    public decimal? OccupiedAreaSf { get; init; }

    /// <summary>Committed area as a percent of total GLA.</summary>
    [JsonPropertyName("occupancy_rate")]
    public decimal? OccupancyRate { get; init; }

    /// <summary>Vacant area as a percent of total GLA.</summary>
    [JsonPropertyName("vacancy_rate")]
    public decimal? VacancyRate { get; init; }

    [JsonPropertyName("est_market_value")]
    public decimal? EstMarketValue { get; init; }

    [JsonPropertyName("est_annual_noi")]
    public decimal? EstAnnualNoi { get; init; }

    [JsonPropertyName("investment_count")]
    public int InvestmentCount { get; init; }
}

/// <summary>Leasing metrics from latest <c>fact_asset_metrics</c> row for GET /api/Assets/{propertyKey}/leasing-summary.</summary>
public sealed class AssetLeasingSummaryDto
{
    [JsonPropertyName("property_key")]
    public long PropertyKey { get; init; }

    [JsonPropertyName("date_key")]
    public int? DateKey { get; init; }

    [JsonPropertyName("last_refreshed_date")]
    public DateTime? LastRefreshedDate { get; init; }

    [JsonPropertyName("gross_leasable_area_sqft")]
    public decimal? GrossLeasableAreaSqft { get; init; }

    [JsonPropertyName("occupied_area_sqft")]
    public decimal? OccupiedAreaSqft { get; init; }

    [JsonPropertyName("committed_area_sqft")]
    public decimal? CommittedAreaSqft { get; init; }

    [JsonPropertyName("vacant_area_sqft")]
    public decimal? VacantAreaSqft { get; init; }

    [JsonPropertyName("total_units")]
    public int? TotalUnits { get; init; }

    [JsonPropertyName("occupied_units")]
    public int? OccupiedUnits { get; init; }

    [JsonPropertyName("vacant_units")]
    public int? VacantUnits { get; init; }

    [JsonPropertyName("weighted_avg_lease_term_months")]
    public decimal? WeightedAvgLeaseTermMonths { get; init; }

    [JsonPropertyName("weighted_avg_lease_term_rent_months")]
    public decimal? WeightedAvgLeaseTermRentMonths { get; init; }

    [JsonPropertyName("gla_available_to_lease_sqft")]
    public decimal? GlaAvailableToLeaseSqft { get; init; }

    [JsonPropertyName("total_leasing_committed_sqft")]
    public decimal? TotalLeasingCommittedSqft { get; init; }

    [JsonPropertyName("new_leasing_committed_sqft")]
    public decimal? NewLeasingCommittedSqft { get; init; }

    [JsonPropertyName("renewal_leasing_committed_sqft")]
    public decimal? RenewalLeasingCommittedSqft { get; init; }

    [JsonPropertyName("gla_available_to_lease_units")]
    public int? GlaAvailableToLeaseUnits { get; init; }

    [JsonPropertyName("total_leasing_committed_units")]
    public int? TotalLeasingCommittedUnits { get; init; }

    [JsonPropertyName("new_leasing_committed_units")]
    public int? NewLeasingCommittedUnits { get; init; }

    [JsonPropertyName("renewal_leasing_committed_units")]
    public int? RenewalLeasingCommittedUnits { get; init; }

    [JsonPropertyName("occupancy_rate")]
    public decimal? OccupancyRate { get; init; }

    [JsonPropertyName("vacancy_rate")]
    public decimal? VacancyRate { get; init; }
}

/// <summary>Property detail overview with dynamic cards.</summary>
public sealed class PropertyDetailDto
{
    public PropertySummaryDto Summary { get; init; } = new();
    public IReadOnlyList<DynamicSectionDto> Sections { get; init; } = [];
}

public sealed class PropertySummaryDto
{
    public long PropertyKey { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string AssetType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>TODO: Map from warehouse query when ownership column is available.</summary>
    public bool Ownership { get; init; }

    /// <summary>Gross leasable area (sf) from latest <c>fact_asset_metrics</c>.</summary>
    public decimal AssetSize { get; init; }

    public bool IsPortfolio { get; init; }
    public object? AcquisitionDate { get; init; }
    public int Investments { get; init; }
}

/// <summary>Associated investments tab on property detail (linked funds).</summary>
public sealed class PropertyInvestmentDto
{
    public int FundKey { get; init; }
    public string FundName { get; init; } = string.Empty;
    public string FundType { get; init; } = string.Empty;
    public string FundStrategy { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? FundStartDate { get; init; }
    public decimal TotalValue { get; init; }
    public decimal? TotalReturnPercent { get; init; }
}
