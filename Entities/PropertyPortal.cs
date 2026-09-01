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

    [JsonPropertyName("asset_type")]
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

/// <summary>Fund holdings grid on asset detail — property linked to current dim_fund row.</summary>
public sealed class PropertyFundHoldingDto
{
    [JsonPropertyName("property_code")]
    public string PropertyCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("fund_strategy_name")]
    public string FundStrategyName { get; init; } = string.Empty;

    [JsonPropertyName("fund_type_name")]
    public string FundTypeName { get; init; } = string.Empty;

    [JsonPropertyName("fund_start_date")]
    public DateTime? FundStartDate { get; init; }
}

/// <summary>
/// Leaf properties under a consolidated asset for GET /api/Assets/{propertyKey}/property-details.
/// </summary>
public sealed class AssetPropertyDetailRowDto
{
    [JsonPropertyName("property_code")]
    public string PropertyCode { get; init; } = string.Empty;

    [JsonPropertyName("property_name")]
    public string PropertyName { get; init; } = string.Empty;

    [JsonPropertyName("asset_to_share_pct")]
    public decimal? AssetToSharePct { get; init; }

    [JsonPropertyName("asset_type")]
    public string AssetType { get; init; } = string.Empty;

    [JsonPropertyName("investment_type")]
    public string InvestmentType { get; init; } = string.Empty;

    [JsonPropertyName("development_type")]
    public string DevelopmentType { get; init; } = string.Empty;

    [JsonPropertyName("gross_leasable_area_sqft")]
    public decimal? GrossLeasableAreaSqft { get; init; }

    [JsonPropertyName("committed_area_sqft")]
    public decimal? CommittedAreaSqft { get; init; }

    [JsonPropertyName("vacant_area_sqft")]
    public decimal? VacantAreaSqft { get; init; }

    [JsonPropertyName("occupancy_rate")]
    public decimal? OccupancyRate { get; init; }

    [JsonPropertyName("vacancy_rate")]
    public decimal? VacancyRate { get; init; }
}

/// <summary>
/// Area metrics grouped by asset type for GET /api/Assets/{propertyKey}/asset-type-summary.
/// </summary>
public sealed class AssetTypeSummaryRowDto
{
    [JsonPropertyName("consolidated_asset_key")]
    public long ConsolidatedAssetKey { get; init; }

    [JsonPropertyName("consolidated_asset_code")]
    public string ConsolidatedAssetCode { get; init; } = string.Empty;

    [JsonPropertyName("consolidated_asset_name")]
    public string ConsolidatedAssetName { get; init; } = string.Empty;

    [JsonPropertyName("asset_type")]
    public string AssetType { get; init; } = string.Empty;

    [JsonPropertyName("gross_leasable_area_sqft")]
    public decimal? GrossLeasableAreaSqft { get; init; }

    [JsonPropertyName("committed_area_sqft")]
    public decimal? CommittedAreaSqft { get; init; }

    [JsonPropertyName("vacant_area_sqft")]
    public decimal? VacantAreaSqft { get; init; }

    [JsonPropertyName("occupancy_rate")]
    public decimal? OccupancyRate { get; init; }

    [JsonPropertyName("vacancy_rate")]
    public decimal? VacancyRate { get; init; }
}

/// <summary>
/// Financial metrics for GET /api/Assets/{propertyKey}/financial-metrics.
/// </summary>
public sealed class AssetFinancialMetricsDto
{
    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("asset_key")]
    public long AssetKey { get; init; }

    [JsonPropertyName("asset_code")]
    public string AssetCode { get; init; } = string.Empty;

    [JsonPropertyName("asset_name")]
    public string AssetName { get; init; } = string.Empty;

    [JsonPropertyName("as_of_date")]
    public DateTime? AsOfDate { get; init; }

    [JsonPropertyName("asset_ks_ownership_pct")]
    public decimal? AssetKsOwnershipPct { get; init; }

    [JsonPropertyName("asset_cash_at_quarter_end")]
    public decimal? AssetCashAtQuarterEnd { get; init; }

    [JsonPropertyName("asset_total_asset_value")]
    public decimal? AssetTotalAssetValue { get; init; }

    [JsonPropertyName("asset_debt")]
    public decimal? AssetDebt { get; init; }

    [JsonPropertyName("asset_equity")]
    public decimal? AssetEquity { get; init; }

    [JsonPropertyName("asset_noi")]
    public decimal? AssetNoi { get; init; }

    [JsonPropertyName("asset_ffo")]
    public decimal? AssetFfo { get; init; }

    [JsonPropertyName("asset_ncf")]
    public decimal? AssetNcf { get; init; }

    [JsonPropertyName("asset_capex")]
    public decimal? AssetCapex { get; init; }

    [JsonPropertyName("asset_nav_amount")]
    public decimal? AssetNavAmount { get; init; }

    [JsonPropertyName("asset_ebitda")]
    public decimal? AssetEbitda { get; init; }

    [JsonPropertyName("asset_revenue")]
    public decimal? AssetRevenue { get; init; }

    [JsonPropertyName("asset_expense")]
    public decimal? AssetExpense { get; init; }

    [JsonPropertyName("asset_gross_market_value")]
    public decimal? AssetGrossMarketValue { get; init; }

    [JsonPropertyName("asset_gav_amount")]
    public decimal? AssetGavAmount { get; init; }

    [JsonPropertyName("asset_ltv")]
    public decimal? AssetLtv { get; init; }

    [JsonPropertyName("asset_affo")]
    public decimal? AssetAffo { get; init; }

    [JsonPropertyName("asset_capex_pct_noi")]
    public decimal? AssetCapexPctNoi { get; init; }

    [JsonPropertyName("total_noi_growth_amount")]
    public decimal? TotalNoiGrowthAmount { get; init; }

    [JsonPropertyName("total_noi_growth_pct")]
    public decimal? TotalNoiGrowthPct { get; init; }

    [JsonPropertyName("same_store_noi_growth_amount")]
    public decimal? SameStoreNoiGrowthAmount { get; init; }

    [JsonPropertyName("same_store_noi_growth_pct")]
    public decimal? SameStoreNoiGrowthPct { get; init; }

    [JsonPropertyName("current_cost_amount")]
    public decimal? CurrentCostAmount { get; init; }

    [JsonPropertyName("cost_basis_amount")]
    public decimal? CostBasisAmount { get; init; }

    [JsonPropertyName("budgeted_noi_current_year")]
    public decimal? BudgetedNoiCurrentYear { get; init; }

    [JsonPropertyName("forecasted_noi_current_year")]
    public decimal? ForecastedNoiCurrentYear { get; init; }

    [JsonPropertyName("budgeted_ffo")]
    public decimal? BudgetedFfo { get; init; }

    [JsonPropertyName("forecasted_ffo")]
    public decimal? ForecastedFfo { get; init; }
}
