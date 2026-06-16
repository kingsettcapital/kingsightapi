namespace kingsightapi.Entities;

using System.Text.Json.Serialization;

/// <summary>Capital Activities row — investor detail, one row per fund.</summary>
public sealed class InvestorFundCapitalActivitiesDto
{
    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

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
    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

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

/// <summary>IRR row — investor detail, one row per fund.</summary>
public sealed class InvestorFundIrrDto
{
    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

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

/// <summary>Capital Activities row — fund detail, one row per investor.</summary>
public sealed class FundInvestorCapitalActivitiesDto
{
    [JsonPropertyName("investor_code")]
    public string InvestorCode { get; init; } = string.Empty;

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

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
