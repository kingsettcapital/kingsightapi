namespace kingsightapi.Entities;

using System.Text.Json.Serialization;

/// <summary>Capital Activities row — investor detail, one row per fund.</summary>
public sealed class InvestorFundCapitalActivitiesDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;

    /// <summary>Quarter label when view is quarterly (e.g. "Q1 2024"). Mirrors <see cref="QuarterYear"/>.</summary>
    [JsonPropertyName("period")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Period { get; init; }

    /// <summary>Fund type from <c>dim_fund.fund_type_name</c>.</summary>
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("called")]
    public decimal Called { get; init; }

    [JsonPropertyName("transfer_in")]
    public decimal TransferIn { get; init; }

    [JsonPropertyName("transfer_out")]
    public decimal TransferOut { get; init; }

    [JsonPropertyName("redemption")]
    public decimal Redemption { get; init; }
}

/// <summary>Distributions row — investor detail, one row per fund.</summary>
public sealed class InvestorFundDistributionsDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;

    /// <summary>Quarter label when view is quarterly (e.g. "Q1 2024").</summary>
    [JsonPropertyName("period")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Period { get; init; }

    /// <summary>Fund type from <c>dim_fund.fund_type_name</c>.</summary>
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("committed")]
    public decimal Committed { get; init; }

    [JsonPropertyName("unfunded")]
    public decimal Unfunded { get; init; }

    [JsonPropertyName("cash_dist")]
    public decimal CashDist { get; init; }

    [JsonPropertyName("gain_dist")]
    public decimal GainDist { get; init; }

    [JsonPropertyName("preferred_return")]
    public decimal PreferredReturn { get; init; }

    [JsonPropertyName("return_of_capital")]
    public decimal ReturnOfCapital { get; init; }

    [JsonPropertyName("released")]
    public decimal Released { get; init; }

    [JsonPropertyName("net_invested_capital_amount")]
    public decimal NetInvestedCapitalAmount { get; init; }

    [JsonPropertyName("net_distributed_amount")]
    public decimal NetDistributedAmount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }
}

/// <summary>IRR row — investor detail, one row per fund.</summary>
public sealed class InvestorFundIrrDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;

    /// <summary>Quarter label when view is quarterly (e.g. "Q1 2024").</summary>
    [JsonPropertyName("period")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Period { get; init; }

    /// <summary>Fund type from <c>dim_fund.fund_type_name</c>.</summary>
    public string Type { get; init; } = string.Empty;

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

    [JsonPropertyName("irr_itd_pct")]
    public decimal? IrrItdPct => IrrLtdPct;
}

/// <summary>Capital obligation row — investor detail, one row per fund (wide portfolio metrics).</summary>
public sealed class InvestorFundObligationDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;

    /// <summary>LTD or quarterly label (e.g. "LTD", "Q1 2024").</summary>
    [JsonPropertyName("period")]
    public string Period { get; init; } = string.Empty;

    [JsonPropertyName("commitment_amount")]
    public decimal CommitmentAmount { get; init; }

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal ReleasedCapitalAmount { get; init; }
}

/// <summary>Net asset value row — investor detail, unitized funds only, quarterly NAV from <c>fact_fund_nav</c>.</summary>
public sealed class InvestorFundNetAssetsDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;

    [JsonPropertyName("period")]
    public string Period { get; init; } = string.Empty;

    public decimal Nav { get; init; }
}

/// <summary>Capital Activities row — fund detail, one row per investor.</summary>
public sealed class FundInvestorCapitalActivitiesDto
{
    [JsonPropertyName("investor_code")]
    public string InvestorCode { get; init; } = string.Empty;

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;

    /// <summary>Quarter label when view is quarterly (e.g. "Q1 2024").</summary>
    [JsonPropertyName("period")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Period { get; init; }

    /// <summary>Investor type from <c>dim_investor.investor_type_name</c>.</summary>
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("called")]
    public decimal Called { get; init; }

    [JsonPropertyName("transfer_in")]
    public decimal TransferIn { get; init; }

    [JsonPropertyName("transfer_out")]
    public decimal TransferOut { get; init; }

    [JsonPropertyName("redemption")]
    public decimal Redemption { get; init; }
}

/// <summary>Distributions row — fund detail, one row per investor.</summary>
public sealed class FundInvestorDistributionsDto
{
    [JsonPropertyName("investor_code")]
    public string InvestorCode { get; init; } = string.Empty;

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;

    /// <summary>Quarter label when view is quarterly (e.g. "Q1 2024").</summary>
    [JsonPropertyName("period")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Period { get; init; }

    /// <summary>Investor type from <c>dim_investor.investor_type_name</c>.</summary>
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("committed")]
    public decimal Committed { get; init; }

    [JsonPropertyName("unfunded")]
    public decimal Unfunded { get; init; }

    [JsonPropertyName("cash_dist")]
    public decimal CashDist { get; init; }

    [JsonPropertyName("gain_dist")]
    public decimal GainDist { get; init; }

    [JsonPropertyName("preferred_return")]
    public decimal PreferredReturn { get; init; }

    [JsonPropertyName("return_of_capital")]
    public decimal ReturnOfCapital { get; init; }

    [JsonPropertyName("released")]
    public decimal Released { get; init; }
}

/// <summary>IRR row — fund detail, one row per investor.</summary>
public sealed class FundInvestorIrrDto
{
    [JsonPropertyName("investor_code")]
    public string InvestorCode { get; init; } = string.Empty;

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;

    /// <summary>Quarter label when view is quarterly (e.g. "Q1 2024").</summary>
    [JsonPropertyName("period")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Period { get; init; }

    /// <summary>Investor type from <c>dim_investor.investor_type_name</c>.</summary>
    public string Type { get; init; } = string.Empty;

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

    [JsonPropertyName("irr_itd_pct")]
    public decimal? IrrItdPct => IrrLtdPct;
}

/// <summary>Capital obligation row — fund detail, one row per investor (wide portfolio metrics).</summary>
public sealed class FundInvestorObligationDto
{
    [JsonPropertyName("investor_code")]
    public string InvestorCode { get; init; } = string.Empty;

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;

    /// <summary>LTD or quarterly label (e.g. "LTD", "Q1 2024").</summary>
    [JsonPropertyName("period")]
    public string Period { get; init; } = string.Empty;

    [JsonPropertyName("commitment_amount")]
    public decimal CommitmentAmount { get; init; }

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal ReleasedCapitalAmount { get; init; }
}

/// <summary>Net asset value row — fund detail, unitized funds only, quarterly fund-level NAV.</summary>
public sealed class FundInvestorNetAssetsDto
{
    [JsonPropertyName("investor_code")]
    public string InvestorCode { get; init; } = string.Empty;

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("quarter_year")]
    public string QuarterYear { get; init; } = string.Empty;

    [JsonPropertyName("period")]
    public string Period { get; init; } = string.Empty;

    public decimal Nav { get; init; }
}
