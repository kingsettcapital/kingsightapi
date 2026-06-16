namespace kingsightapi.Entities;

using System.Text.Json.Serialization;

/// <summary>Property row on the module list page (Assets tab).</summary>
public sealed class PropertyListItemDto
{
    public long PropertyKey { get; init; }

    [JsonPropertyName("property_code")]
    public string PropertyCode { get; init; } = string.Empty;

    public string PropertyName { get; init; } = string.Empty;
    public string Geography { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Province { get; init; } = string.Empty;

    public string AssetType { get; init; } = string.Empty;

    [JsonPropertyName("investment_type")]
    public string InvestmentType { get; init; } = string.Empty;

    [JsonPropertyName("development_type")]
    public string DevelopmentType { get; init; } = string.Empty;

    [JsonPropertyName("property_status")]
    public string PropertyStatus { get; init; } = string.Empty;

    /// <summary>Gross leasable area (sf) from latest <c>fact_asset_metrics</c> row.</summary>
    [JsonPropertyName("gla_sf")]
    public decimal? GlaSf { get; init; }

    [JsonPropertyName("occupied_sf")]
    public decimal? OccupiedSf { get; init; }

    [JsonPropertyName("committed_sf")]
    public decimal? CommittedSf { get; init; }

    [JsonPropertyName("vacant_sf")]
    public decimal? VacantSf { get; init; }

    /// <summary>Legacy alias for <see cref="PropertyStatus"/>.</summary>
    public string Status => PropertyStatus;

    public bool IsPortfolio { get; init; }
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

    /// <summary>TODO: Map from warehouse query when ownership column is available.</summary>
    public bool Ownership { get; init; }

    /// <summary>Gross leasable area (sf) from latest <c>fact_asset_metrics</c>.</summary>
    public decimal AssetSize { get; init; }

    public bool IsPortfolio { get; init; }
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
