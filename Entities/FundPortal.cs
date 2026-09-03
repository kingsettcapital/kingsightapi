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

    public int Investors { get; init; }
    public int Assets { get; init; }

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

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("invested_percent")]
    public decimal? InvestedPercent { get; init; }

    /// <summary>Legacy alias for <see cref="FundStrategyName"/>.</summary>
    public string Category => FundStrategyName;

    /// <summary>Legacy alias for <see cref="NetInvestedCapitalAmount"/>.</summary>
    public decimal CurrentValue => NetInvestedCapitalAmount;
}

/// <summary>Flat fund profile header for GET /api/Funds/{fundKey}.</summary>
public sealed class FundProfileDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("fund_type")]
    public string FundType { get; init; } = string.Empty;

    public string Strategy { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("start_date")]
    public DateTime? StartDate { get; init; }

    [JsonPropertyName("is_sidecar")]
    public bool IsSidecar { get; init; }

    [JsonPropertyName("total_commitment")]
    public decimal TotalCommitment { get; init; }

    [JsonPropertyName("net_invested_capital")]
    public decimal NetInvestedCapital { get; init; }

    [JsonPropertyName("net_distributed")]
    public decimal NetDistributed { get; init; }

    [JsonPropertyName("reserved_uncalled")]
    public decimal ReservedUncalled { get; init; }

    [JsonPropertyName("unfunded")]
    public decimal Unfunded { get; init; }

    [JsonPropertyName("released_capital")]
    public decimal? ReleasedCapital { get; init; }

    [JsonPropertyName("capital_deployed")]
    public decimal CapitalDeployed { get; init; }

    [JsonPropertyName("investor_count")]
    public int InvestorCount { get; init; }

    [JsonPropertyName("asset_count")]
    public int AssetCount { get; init; }

    public IReadOnlyList<FundProfileInvestorDto> Investors { get; init; } = [];
}

public sealed class FundProfileInvestorDto
{
    [JsonPropertyName("investor_key")]
    public long InvestorKey { get; init; }

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;
}

/// <summary>Fund aggregates for a single fund key.</summary>
public sealed class FundDetailDto
{
    public FundSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<DynamicSectionDto> Sections { get; init; } = [];
}

public sealed class FundSummaryDto
{
    [JsonPropertyName("fund_id")]
    public int FundId { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("fund_type_name")]
    public string FundType { get; init; } = string.Empty;

    [JsonPropertyName("fund_type")]
    public string FundTypeLabel => FundType;

    public string Status { get; init; } = string.Empty;

    public int Assets { get; init; }
    public int Investors { get; init; }

    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("commitment")]
    public decimal Commitment { get; init; }

    [JsonPropertyName("commitment_amount")]
    public decimal CommitmentAmount => Commitment;

    [JsonPropertyName("called")]
    public decimal Called { get; init; }

    [JsonPropertyName("netinvestedamount")]
    public decimal Netinvestedamount { get; init; }

    [JsonPropertyName("net_invested_capital_amount")]
    public decimal NetInvestedCapitalAmount => Netinvestedamount;

    [JsonPropertyName("net_invested_capital")]
    public decimal NetInvestedCapital => Netinvestedamount;

    [JsonPropertyName("netinvestedunits")]
    public decimal Netinvestedunits { get; init; }

    [JsonPropertyName("reserveamount")]
    public decimal Reserveamount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount => Reserveamount;

    [JsonPropertyName("fund_strategy_name")]
    public string FundStrategyName { get; init; } = string.Empty;

    [JsonPropertyName("strategy")]
    public string Strategy => FundStrategyName;

    [JsonPropertyName("net_distributed")]
    public decimal NetDistributed { get; init; }

    [JsonPropertyName("net_distributed_amount")]
    public decimal NetDistributedAmount => NetDistributed;

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal ReleasedCapitalAmount { get; init; }

    [JsonPropertyName("released_capital")]
    public decimal ReleasedCapital => ReleasedCapitalAmount;

    [JsonPropertyName("current_value")]
    public decimal CurrentValue { get; init; }

    [JsonPropertyName("total_return_percent")]
    public decimal? TotalReturnPercent { get; init; }

    [JsonPropertyName("invested_percent")]
    public decimal? InvestedPercent { get; init; }

    /// <summary>Distributed to Paid-In: net distributed / net invested capital.</summary>
    [JsonPropertyName("dpi")]
    public decimal? Dpi =>
        Netinvestedamount > 0m
            ? Math.Round(NetDistributed / Netinvestedamount, 4, MidpointRounding.AwayFromZero)
            : null;

    /// <summary>Total value to Paid-In: (current value or NIC + distributions) / net invested capital.</summary>
    [JsonPropertyName("tvpi")]
    public decimal? Tvpi
    {
        get
        {
            if (Netinvestedamount <= 0m)
            {
                return null;
            }

            var totalValue = CurrentValue > 0m ? CurrentValue : Netinvestedamount + NetDistributed;
            return Math.Round(totalValue / Netinvestedamount, 4, MidpointRounding.AwayFromZero);
        }
    }
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

    [JsonPropertyName("gla_sf")]
    public decimal? GlaSf { get; init; }

    [JsonPropertyName("occupancy_pct")]
    public decimal? OccupancyPct { get; init; }

    [JsonPropertyName("market_value")]
    public decimal? MarketValue { get; init; }

    [JsonPropertyName("cap_rate")]
    public decimal? CapRate { get; init; }

    public string Status => PropertyStatus;
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
    [JsonPropertyName("fund_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FundKey { get; init; }

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
    [JsonPropertyName("fund_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FundKey { get; init; }

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
