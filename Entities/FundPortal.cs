namespace kingsightapi.Entities;

using System.Text.Json.Serialization;

/// <summary>Fund sidebar row (investments list screen).</summary>
public sealed class FundListItemDto
{
    public int FundKey { get; init; }
    public string FundName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal CurrentValue { get; init; }
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
    public string InvestorType { get; init; } = string.Empty;
    public decimal TotalInvested { get; init; }
    public decimal TotalInvestedFmv { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? MemberSince { get; init; }
    public int? JoinYear { get; init; }
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
    /// <summary>LTD / quarterly label (e.g. "Life To Date", "Q4 2022"). Null for daily.</summary>
    public string? Period { get; init; }

    /// <summary>Date for daily view (commitments: from posted date). Null for LTD / quarterly.</summary>
    public DateTime? Date { get; init; }

    [JsonPropertyName("posted_date_key")]
    public int? PostedDateKey { get; init; }

    public decimal Amount { get; init; }

    public decimal Units { get; init; }

    public string Description { get; init; } = string.Empty;
}
