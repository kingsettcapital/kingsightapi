namespace kingsightapi.Entities;

using System.Text.Json.Serialization;

/// <summary>Investor row on the module list page (Investors tab).</summary>
public sealed class InvestorListItemDto
{
    public long InvestorKey { get; init; }
    public string InvestorName { get; init; } = string.Empty;
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

    [JsonPropertyName("released_capital_amount")]
    public decimal ReleasedCapitalAmount { get; init; }

    /// <summary>Legacy alias for <see cref="NetInvestedCapitalAmount"/>.</summary>
    public decimal TotalInvested => NetInvestedCapitalAmount;
}

/// <summary>Investor profile header + overview tab.</summary>
public sealed class InvestorDetailDto
{
    public InvestorSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<DynamicSectionDto> Sections { get; init; } = [];
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
}
