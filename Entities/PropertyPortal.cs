namespace kingsightapi.Entities;

/// <summary>Asset sidebar row (property list screen).</summary>
public sealed class PropertyListItemDto
{
    public long PropertyKey { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Province { get; init; } = string.Empty;
    public string AssetType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal CurrentValue { get; init; }
    public decimal? YieldPercent { get; init; }
}

/// <summary>Property detail overview with dynamic cards.</summary>
public sealed class PropertyDetailDto
{
    public PropertySummaryDto Summary { get; init; } = new();
    public IReadOnlyList<DynamicSectionDto> Sections { get; init; } = [];
}

public sealed class PropertySummaryDto
{
    public long PropertyKey { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string AssetType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal CurrentValue { get; init; }
    public decimal? Yield { get; init; }
    public object? AcquisitionDate { get; init; }
    public int Investments { get; init; }
}

/// <summary>Associated investments tab on property detail (linked funds).</summary>
public sealed class PropertyInvestmentDto
{
    public int FundKey { get; init; }
    public string FundName { get; init; } = string.Empty;
    public string FundType { get; init; } = string.Empty;
    public string FundStrategy { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? FundStartDate { get; init; }
    public decimal TotalValue { get; init; }
    public decimal? TotalReturnPercent { get; init; }
}
