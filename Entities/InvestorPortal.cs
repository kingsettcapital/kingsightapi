namespace kingsightapi.Entities;

/// <summary>Investor sidebar row (profile list screen).</summary>
public sealed class InvestorListItemDto
{
    public long InvestorKey { get; init; }
    public string InvestorName { get; init; } = string.Empty;
    public string InvestorType { get; init; } = string.Empty;
    public decimal TotalInvested { get; init; }
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
    public string FundName { get; init; } = string.Empty;
    public string FundType { get; init; } = string.Empty;
    public string FundCategory { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal InvestedAmount { get; init; }
    public decimal InvestedAmountFmv { get; init; }
    public decimal? TotalReturnPercent { get; init; }
}
