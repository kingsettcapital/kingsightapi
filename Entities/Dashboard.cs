using System.Text.Json.Serialization;

namespace kingsightapi.Entities;

/// <summary>Widget id constants — must match Angular dashboard picker.</summary>
public static class DashboardWidgetIds
{
    public const int MaxPerRequest = 5;

    public const string PortfolioValue = "portfolioValue";
    public const string ActiveFunds = "activeFunds";
    public const string TotalAum = "totalAum";
    public const string YtdReturns = "ytdReturns";
    public const string InvestorCount = "investorCount";
    public const string AssetCount = "assetCount";
    public const string PerformanceChart = "performanceChart";
    public const string AssetAllocation = "assetAllocation";
    public const string FundReturns = "fundReturns";
    public const string InvestorGrowth = "investorGrowth";
    public const string GeographicDistribution = "geographicDistribution";

    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PortfolioValue] = "Total Equity Under Management",
            [ActiveFunds] = "Active Funds",
            [TotalAum] = "Total AUM",
            [YtdReturns] = "YTD Returns",
            [InvestorCount] = "Investor Count",
            [AssetCount] = "Asset Count",
            [PerformanceChart] = "Performance Chart",
            [AssetAllocation] = "Asset Allocation",
            [FundReturns] = "Fund Returns",
            [InvestorGrowth] = "Investor Growth",
            [GeographicDistribution] = "Geographic Distribution"
        };

    public static readonly IReadOnlyList<string> All =
    [
        PortfolioValue,
        ActiveFunds,
        TotalAum,
        YtdReturns,
        InvestorCount,
        AssetCount,
        PerformanceChart,
        AssetAllocation,
        FundReturns,
        InvestorGrowth,
        GeographicDistribution
    ];

    public static bool IsKnown(string id) => Labels.ContainsKey(id);

    public static string GetLabel(string id) =>
        Labels.TryGetValue(id, out var label) ? label : id;

    public static IReadOnlyList<DashboardWidgetOptionDto> GetCatalog() =>
        All.Select(id => new DashboardWidgetOptionDto { Id = id, Label = Labels[id] }).ToList();

    /// <summary>Validates widget ids for GET /api/dashboard (1–5 known ids, no duplicates).</summary>
    public static bool TryParseWidgetQuery(
        string? widgets,
        out IReadOnlyList<string> widgetIds,
        out string errorMessage)
    {
        widgetIds = [];
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(widgets))
        {
            errorMessage = "Query parameter 'widgets' is required (comma-separated widget ids, max 5).";
            return false;
        }

        var parsed = widgets
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (parsed.Count == 0)
        {
            errorMessage = "Query parameter 'widgets' must include at least one widget id.";
            return false;
        }

        if (parsed.Count > MaxPerRequest)
        {
            errorMessage = $"At most {MaxPerRequest} widget ids are allowed per request.";
            return false;
        }

        var unique = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in parsed)
        {
            if (!IsKnown(id))
            {
                errorMessage = $"Unknown widget id '{id}'. Call GET /api/dashboard/widgets for valid ids.";
                return false;
            }

            if (seen.Add(id))
            {
                unique.Add(Labels.Keys.First(k => string.Equals(k, id, StringComparison.OrdinalIgnoreCase)));
            }
        }

        widgetIds = unique;
        return true;
    }
}

/// <summary>Widget picker option for GET /api/dashboard/widgets.</summary>
public sealed class DashboardWidgetOptionDto
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

/// <summary>Single response for the Kingsight dashboard (all widgets).</summary>
public sealed class DashboardResponseDto
{
    public DateTime LastUpdated { get; init; }
    public int CalendarYear { get; init; }

    /// <summary>Only requested widgets are populated; omitted keys are null.</summary>
    public DashboardWidgetsDto Widgets { get; init; } = new();
}

public sealed record DashboardWidgetsDto
{
    public DashboardKpiWidgetDto? PortfolioValue { get; init; }
    public DashboardKpiWidgetDto? ActiveFunds { get; init; }
    public DashboardKpiWidgetDto? TotalAum { get; init; }
    public DashboardKpiWidgetDto? YtdReturns { get; init; }
    public DashboardKpiWidgetDto? InvestorCount { get; init; }
    public DashboardKpiWidgetDto? AssetCount { get; init; }
    public DashboardLineChartWidgetDto? PerformanceChart { get; init; }
    public DashboardDonutWidgetDto? AssetAllocation { get; init; }
    public DashboardGroupedBarWidgetDto? FundReturns { get; init; }
    public DashboardLineChartWidgetDto? InvestorGrowth { get; init; }
    public DashboardHorizontalBarWidgetDto? GeographicDistribution { get; init; }
}

/// <summary>KPI card (portfolio value, fund count, etc.).</summary>
public sealed class DashboardKpiWidgetDto
{
    public decimal Value { get; init; }

    [JsonPropertyName("ytdChange")]
    public decimal? YtdChange { get; init; }

    [JsonPropertyName("ytdChangePercent")]
    public decimal? YtdChangePercent { get; init; }

    public string Subtitle { get; init; } = string.Empty;

    /// <summary>money | count | percent</summary>
    public string Format { get; init; } = "count";
}

public sealed class DashboardLineChartWidgetDto
{
    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<DashboardChartSeriesDto> Series { get; init; } = [];
}

public sealed class DashboardChartSeriesDto
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<decimal?> Values { get; init; } = [];
}

public sealed class DashboardDonutWidgetDto
{
    public IReadOnlyList<DashboardDonutSliceDto> Slices { get; init; } = [];
}

public sealed class DashboardDonutSliceDto
{
    public string Label { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public decimal SharePercent { get; init; }
}

public sealed class DashboardGroupedBarWidgetDto
{
    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<DashboardChartSeriesDto> Series { get; init; } = [];
}

public sealed class DashboardHorizontalBarWidgetDto
{
    public IReadOnlyList<DashboardHorizontalBarItemDto> Items { get; init; } = [];
}

public sealed class DashboardHorizontalBarItemDto
{
    public string Label { get; init; } = string.Empty;
    public decimal SharePercent { get; init; }
}
