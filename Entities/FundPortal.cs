namespace kingsightapi.Entities;

using System.Text.Json.Serialization;

/// <summary>Fund row on the module list page (Investments tab).</summary>
public sealed class FundListItemDto
{
    public int FundKey { get; init; }
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("fund_type_name")]
    public string FundTypeName { get; init; } = string.Empty;

    [JsonPropertyName("fund_strategy_name")]
    public string FundStrategyName { get; init; } = string.Empty;

    [JsonPropertyName("commitment_amount")]
    public decimal CommitmentAmount { get; init; }

    [JsonPropertyName("net_invested_capital_amount")]
    public decimal NetInvestedCapitalAmount { get; init; }

    [JsonPropertyName("net_distributed_amount")]
    public decimal NetDistributedAmount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal ReleasedCapitalAmount { get; init; }

    /// <summary>Legacy alias for <see cref="FundStrategyName"/>.</summary>
    public string Category => FundStrategyName;

    /// <summary>Legacy alias for <see cref="NetInvestedCapitalAmount"/>.</summary>
    public decimal CurrentValue => NetInvestedCapitalAmount;
}

/// <summary>Fund aggregates for a single fund key.</summary>
public sealed class FundDetailDto
{
    public FundSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<DynamicSectionDto> Sections { get; init; } = [];
}

public sealed class FundSummaryDto
{
    public int FundId { get; init; }
    public string FundCode { get; init; } = string.Empty;
    public string FundName { get; init; } = string.Empty;
    public string FundType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Assets { get; init; }
    public int Investors { get; init; }

    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("commitment")]
    public decimal Commitment { get; init; }

    [JsonPropertyName("called")]
    public decimal Called { get; init; }

    [JsonPropertyName("netinvestedamount")]
    public decimal Netinvestedamount { get; init; }

    [JsonPropertyName("netinvestedunits")]
    public decimal Netinvestedunits { get; init; }

    [JsonPropertyName("reserveamount")]
    public decimal Reserveamount { get; init; }
}

/// <summary>Investors tab on fund detail.</summary>
public sealed class FundInvestorDto
{
    public long InvestorKey { get; init; }

    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("relationship_name")]
    public string RelationshipName { get; init; } = string.Empty;

    public string InvestorType { get; init; } = string.Empty;

    [JsonPropertyName("contact_first_name")]
    public string ContactFirstName { get; init; } = string.Empty;

    [JsonPropertyName("contact_last_name")]
    public string ContactLastName { get; init; } = string.Empty;

    public decimal TotalInvested { get; init; }

    public decimal TotalInvestedFmv { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime? MemberSince { get; init; }

    public int? JoinYear { get; init; }
}

/// <summary>Assets (dim_property) tab on fund detail.</summary>
public sealed class FundAssetDto
{
    public long PropertyKey { get; init; }

    [JsonPropertyName("property_name")]
    public string PropertyName { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;

    public string Geography { get; init; } = string.Empty;

    [JsonPropertyName("asset_type")]
    public string AssetType { get; init; } = string.Empty;

    [JsonPropertyName("investment_type")]
    public string InvestmentType { get; init; } = string.Empty;

    [JsonPropertyName("property_status")]
    public string PropertyStatus { get; init; } = string.Empty;

    [JsonPropertyName("property_acquisition")]
    public string? PropertyAcquisition { get; init; }

    [JsonPropertyName("property_disposition")]
    public string? PropertyDisposition { get; init; }
}

/// <summary>Period dropdown option (quarter or day) scoped by fund, view, and metric.</summary>
public sealed class FundPeriodDto
{
    [JsonPropertyName("date_key")]
    public int? DateKey { get; init; }

    [JsonPropertyName("full_date")]
    public DateTime? FullDate { get; init; }

    public string Label { get; init; } = string.Empty;

    /// <summary>When true, UI should show the option but keep the period control disabled (LTD).</summary>
    public bool Disabled { get; init; }

    [JsonPropertyName("quarter_year")]
    public string? QuarterYear { get; init; }

    [JsonPropertyName("calendar_year")]
    public int CalendarYear { get; init; }

    [JsonPropertyName("month_year")]
    public string? MonthYear { get; init; }

    [JsonPropertyName("period_start")]
    public DateTime? PeriodStart { get; init; }

    [JsonPropertyName("period_end")]
    public DateTime? PeriodEnd { get; init; }
}

/// <summary>Fund table row for LTD, quarterly, or daily views (commitments, NAV, etc.).</summary>
public sealed class FundGranularRowDto
{
    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("investor_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InvestorCode { get; init; }

    /// <summary>LTD / quarterly label (e.g. "Life To Date", "Q4 2022"). Null for daily.</summary>
    public string? Period { get; init; }

    /// <summary>Distribution rows: expandable group header from dim_transaction_type.</summary>
    [JsonPropertyName("transaction_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransactionType { get; init; }

    /// <summary>Date for daily view. Null for LTD / quarterly.</summary>
    public DateTime? Date { get; init; }

    [JsonPropertyName("posted_date_key")]
    public int? PostedDateKey { get; init; }

    [JsonPropertyName("commitment_amount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? CommitmentAmount { get; init; }

    [JsonPropertyName("invested_amount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? InvestedAmount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Amount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Units { get; init; }

    public string Description { get; init; } = string.Empty;
}

/// <summary>One period (or day) line within a distribution transaction-type group.</summary>
public sealed class FundDistributionPeriodRowDto
{
    public string? Period { get; init; }

    public DateTime? Date { get; init; }

    [JsonPropertyName("posted_date_key")]
    public int? PostedDateKey { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Amount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Units { get; init; }

    public string Description { get; init; } = string.Empty;
}

/// <summary>Distributions tab: rows grouped by dim_transaction_type for expandable UI sections.</summary>
public sealed class FundDistributionGroupDto
{
    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("investor_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InvestorCode { get; init; }

    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; init; } = string.Empty;

    public IReadOnlyList<FundDistributionPeriodRowDto> Periods { get; init; } = [];

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("total_units")]
    public decimal TotalUnits { get; init; }
}
