using System.Text.Json.Serialization;

namespace kingsightapi.Entities;

/// <summary>List page response with KPI summary cards plus paginated table rows.</summary>
public sealed class PortalListPageResult<TItem, TSummary>
{
    public IReadOnlyList<TItem> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public TSummary Summary { get; init; } = default!;
}

/// <summary>Investors list page KPI row (Figma summary cards).</summary>
public sealed class InvestorListSummaryDto
{
    [JsonPropertyName("total_investors")]
    public int TotalInvestors { get; init; }

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
    public decimal ReleasedCapital { get; init; }
}

/// <summary>Investments list page KPI row (Figma summary cards).</summary>
public sealed class FundListSummaryDto
{
    [JsonPropertyName("total_funds")]
    public int TotalFunds { get; init; }

    [JsonPropertyName("total_commitment")]
    public decimal TotalCommitment { get; init; }

    [JsonPropertyName("net_invested_capital")]
    public decimal NetInvestedCapital { get; init; }

    [JsonPropertyName("net_distributed")]
    public decimal NetDistributed { get; init; }

    [JsonPropertyName("reserved_uncalled")]
    public decimal ReservedUncalled { get; init; }
}

/// <summary>Assets list page KPI row (Figma summary cards).</summary>
public sealed class AssetListSummaryDto
{
    [JsonPropertyName("total_gla_sf")]
    public decimal TotalGlaSf { get; init; }

    [JsonPropertyName("active_properties")]
    public int ActiveProperties { get; init; }

    [JsonPropertyName("total_properties")]
    public int TotalProperties { get; init; }

    [JsonPropertyName("total_committed_sf")]
    public decimal TotalCommittedSf { get; init; }

    [JsonPropertyName("total_vacant_sf")]
    public decimal TotalVacantSf { get; init; }
}
