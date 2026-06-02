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

/// <summary>NAV rows for a fund ordered by date key.</summary>
public sealed class FundNavDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("date_key")]
    public int DateKey { get; init; }

    [JsonPropertyName("nav")]
    public decimal Nav { get; init; }
}
