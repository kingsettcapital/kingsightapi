namespace kingsightapi.Entities;

/// <summary>Fund sidebar row (investments list screen).</summary>
public sealed class FundListItemDto
{
    public int FundKey { get; init; }
    public string FundName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal CurrentValue { get; init; }
    public decimal? TotalReturnPercent { get; init; }
}

/// <summary>Fund detail header + overview tab.</summary>
public sealed class FundDetailDto
{
    public FundSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<DynamicSectionDto> Sections { get; init; } = [];
}

public sealed class FundSummaryDto
{
    public int FundKey { get; init; }
    public int FundId { get; init; }
    public string FundCode { get; init; } = string.Empty;
    public string FundName { get; init; } = string.Empty;
    public string FundType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal TotalValue { get; init; }
    public decimal CurrentValue { get; init; }
    public decimal? CapitalDeployed { get; init; }
    public decimal? TotalReturnPercent { get; init; }
    public int Assets { get; init; }
    public int Investors { get; init; }
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
