namespace kingsightapi.Entities;

using System.Text.Json.Serialization;

/// <summary>Investor row on the module list page (Investors tab).</summary>
public sealed class InvestorListItemDto
{
    [JsonPropertyName("investor_key")]
    public long InvestorKey { get; init; }

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("investor_type")]
    public string InvestorType { get; init; } = string.Empty;

    [JsonPropertyName("relationship_name")]
    public string RelationshipName { get; init; } = string.Empty;

    [JsonPropertyName("contact_first_name")]
    public string ContactFirstName { get; init; } = string.Empty;

    [JsonPropertyName("contact_last_name")]
    public string ContactLastName { get; init; } = string.Empty;

    [JsonPropertyName("fund_count")]
    public int FundCount { get; init; }

    [JsonPropertyName("commitment_amount")]
    public decimal CommitmentAmount { get; init; }

    [JsonPropertyName("net_invested_capital_amount")]
    public decimal NetInvestedCapitalAmount { get; init; }

    [JsonPropertyName("net_distributed_amount")]
    public decimal NetDistributedAmount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal? ReleasedCapitalAmount { get; init; }

    /// <summary>Legacy alias for <see cref="NetInvestedCapitalAmount"/>.</summary>
    public decimal TotalInvested => NetInvestedCapitalAmount;
}

/// <summary>Investor profile header + overview tab.</summary>
public sealed class InvestorDetailDto
{
    public InvestorSummaryDto Summary { get; init; } = new();

    /// <summary>Period-scoped KPI totals for drill-down cards (refresh-safe).</summary>
    public InvestorDetailMetricsDto? Metrics { get; init; }

    public IReadOnlyList<DynamicSectionDto> Sections { get; init; } = [];
}

/// <summary>Investor-level portfolio KPIs for a view/period (matches list aggregates).</summary>
public sealed class InvestorDetailMetricsDto
{
    [JsonPropertyName("total_commitment")]
    public decimal TotalCommitment { get; init; }

    [JsonPropertyName("net_invested_capital")]
    public decimal NetInvestedCapital { get; init; }

    [JsonPropertyName("net_distributed")]
    public decimal NetDistributed { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal? ReleasedCapitalAmount { get; init; }

    [JsonPropertyName("fund_count")]
    public int FundCount { get; init; }
}

/// <summary>Per-fund exposure row for investor drill-down Fund Exposure grid.</summary>
public sealed class InvestorFundExposureDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("commitment_amount")]
    public decimal CommitmentAmount { get; init; }

    [JsonPropertyName("net_invested_capital_amount")]
    public decimal NetInvestedCapitalAmount { get; init; }

    [JsonPropertyName("net_distributed_amount")]
    public decimal NetDistributedAmount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal? ReleasedCapitalAmount { get; init; }

    [JsonPropertyName("invested_percent")]
    public decimal? InvestedPercent { get; init; }
}

/// <summary>Underlying asset row for investor drill-down.</summary>
public sealed class InvestorUnderlyingAssetDto
{
    [JsonPropertyName("property_key")]
    public long PropertyKey { get; init; }

    [JsonPropertyName("property_name")]
    public string PropertyName { get; init; } = string.Empty;

    [JsonPropertyName("asset_type")]
    public string AssetType { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("gla_sf")]
    public decimal? GlaSf { get; init; }

    [JsonPropertyName("occupancy_pct")]
    public decimal? OccupancyPct { get; init; }

    [JsonPropertyName("market_value")]
    public decimal? MarketValue { get; init; }

    [JsonPropertyName("cap_rate")]
    public decimal? CapRate { get; init; }

    public string Status { get; init; } = string.Empty;
}

public sealed class InvestorSummaryDto
{
    public long InvestorKey { get; init; }
    public int InvestorId { get; init; }
    public string InvestorName { get; init; } = string.Empty;
    public string InvestorType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal TotalInvested { get; init; }
    public int InvestmentsCount { get; init; }
    public int DocumentsCount { get; init; }
    public int? JoinYear { get; init; }
}

/// <summary>Investments tab on investor profile (one row per fund).</summary>
public sealed class InvestorInvestmentDto
{
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    public string FundName { get; init; } = string.Empty;
    public string FundType { get; init; } = string.Empty;
    public string FundCategory { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal InvestedAmount { get; init; }
    public decimal InvestedAmountFmv { get; init; }
    public decimal? TotalReturnPercent { get; init; }

    [JsonPropertyName("commitment_amount")]
    public decimal? CommitmentAmount { get; init; }

    [JsonPropertyName("net_invested_capital_amount")]
    public decimal? NetInvestedCapitalAmount { get; init; }

    [JsonPropertyName("net_distributed_amount")]
    public decimal? NetDistributedAmount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal? ReservedAmount { get; init; }

    [JsonPropertyName("unfunded_amount")]
    public decimal? UnfundedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal? ReleasedCapitalAmount { get; init; }

    [JsonPropertyName("invested_percent")]
    public decimal? InvestedPercent { get; init; }
}

/// <summary>Investor × fund subscription detail (composite page).</summary>
public sealed class InvestorFundSubscriptionDetailDto
{
    public InvestorFundSubscriptionSummaryDto Summary { get; init; } = new();

    [JsonPropertyName("capital_account")]
    public InvestorFundCapitalAccountDto CapitalAccount { get; init; } = new();

    public InvestorFundSubscriptionPerformanceDto Performance { get; init; } = new();

    [JsonPropertyName("capital_activities")]
    public InvestorFundCapitalActivitiesBlockDto CapitalActivities { get; init; } = new();

    public InvestorFundDistributionsBlockDto Distributions { get; init; } = new();

    public InvestorFundIrrBlockDto Irr { get; init; } = new();
}

public sealed class InvestorFundSubscriptionSummaryDto
{
    [JsonPropertyName("investor_key")]
    public long InvestorKey { get; init; }

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("fund_type")]
    public string FundType { get; init; } = string.Empty;

    [JsonPropertyName("fund_id")]
    public int FundId { get; init; }

    public string Status { get; init; } = string.Empty;
}

public sealed class InvestorFundCapitalAccountDto
{
    [JsonPropertyName("total_commitment")]
    public decimal TotalCommitment { get; init; }

    [JsonPropertyName("net_invested_capital")]
    public decimal NetInvestedCapital { get; init; }

    [JsonPropertyName("net_distributed")]
    public decimal NetDistributed { get; init; }

    [JsonPropertyName("reserved_uncalled")]
    public decimal ReservedUncalled { get; init; }

    [JsonPropertyName("released_capital")]
    public decimal ReleasedCapital { get; init; }

    [JsonPropertyName("invested_percent")]
    public decimal? InvestedPercent { get; init; }

    [JsonPropertyName("total_value")]
    public decimal TotalValue { get; init; }

    public decimal? Tvpi { get; init; }
}

public sealed class InvestorFundSubscriptionPerformanceDto
{
    public decimal? Tvpi { get; init; }
    public decimal? Dpi { get; init; }
    public decimal? Rvpi { get; init; }

    [JsonPropertyName("deployment_percent")]
    public decimal? DeploymentPercent { get; init; }
}

public sealed class InvestorFundCapitalActivitiesBlockDto
{
    public decimal Called { get; init; }

    [JsonPropertyName("transfer_in")]
    public decimal TransferIn { get; init; }

    [JsonPropertyName("transfer_out")]
    public decimal TransferOut { get; init; }

    public decimal Redemption { get; init; }
}

public sealed class InvestorFundDistributionsBlockDto
{
    [JsonPropertyName("preferred_return")]
    public decimal PreferredReturn { get; init; }

    [JsonPropertyName("cash_dist")]
    public decimal CashDist { get; init; }

    [JsonPropertyName("gain_dist")]
    public decimal GainDist { get; init; }

    [JsonPropertyName("return_of_capital")]
    public decimal ReturnOfCapital { get; init; }
}

public sealed class InvestorFundIrrBlockDto
{
    [JsonPropertyName("irr_1_year_pct")]
    public decimal? Irr1YearPct { get; init; }

    [JsonPropertyName("irr_3_year_pct")]
    public decimal? Irr3YearPct { get; init; }

    [JsonPropertyName("irr_5_year_pct")]
    public decimal? Irr5YearPct { get; init; }

    [JsonPropertyName("irr_7_year_pct")]
    public decimal? Irr7YearPct { get; init; }

    [JsonPropertyName("irr_10_year_pct")]
    public decimal? Irr10YearPct { get; init; }

    [JsonPropertyName("irr_ltd_pct")]
    public decimal? IrrLtdPct { get; init; }
}
