using kingsightapi.Entities;

namespace kingsightapi.Configuration;

/// <summary>
/// In-memory section catalog (stand-in for dbo.dashboard_section until DB tables exist).
/// Add or disable sections here; the API surface stays stable when rows move to SQL.
/// </summary>
internal static class DashboardSectionRegistry
{
    private static readonly IReadOnlyList<SectionDefinition> Definitions =
    [
        // Investors — KPI row (eager)
        Section(DashboardSectionId.InvestorsKpiSummary, DashboardModule.Investors,
            "Summary", null, DashboardSectionLayout.KpiRow, DashboardSectionLoadStrategy.Eager, 0),

        // Investors — accordion sections (lazy, field payloads)
        Section(DashboardSectionId.InvestorsAnalyticsBenchmarking, DashboardModule.Investors,
            "Investor Analytics & Benchmarking", null, DashboardSectionLayout.GroupedFields,
            DashboardSectionLoadStrategy.Lazy, 10),
        Section(DashboardSectionId.InvestorsCapitalAccountSummary, DashboardModule.Investors,
            "Capital Account Summary", null, DashboardSectionLayout.Fields,
            DashboardSectionLoadStrategy.Lazy, 20),
        Section(DashboardSectionId.InvestorsRiskCompliance, DashboardModule.Investors,
            "Risk & Compliance", null, DashboardSectionLayout.Fields,
            DashboardSectionLoadStrategy.Lazy, 30),
        Section(DashboardSectionId.InvestorsReportingCommunications, DashboardModule.Investors,
            "Reporting & Communications", null, DashboardSectionLayout.Fields,
            DashboardSectionLoadStrategy.Lazy, 40),
        Section(DashboardSectionId.InvestorsPortalAccess, DashboardModule.Investors,
            "Investor Portal Access", null, DashboardSectionLayout.Fields,
            DashboardSectionLoadStrategy.Lazy, 50),

        // Investors — data tables (lazy; rows from dedicated list endpoints)
        Section(DashboardSectionId.InvestorsList, DashboardModule.Investors,
            "Investor List", "Limited partner database", DashboardSectionLayout.Table,
            DashboardSectionLoadStrategy.Lazy, 60, dataRoute: "/api/CapitalInvestors"),
        Section(DashboardSectionId.InvestorsTransactions, DashboardModule.Investors,
            "Transactions", "Recent capital activity across all investors", DashboardSectionLayout.Table,
            DashboardSectionLoadStrategy.Lazy, 70, dataRoute: "/api/dashboard/modules/investors/transactions"),

        // Investments module
        Section(DashboardSectionId.InvestmentsKpiSummary, DashboardModule.Investments,
            "Summary", null, DashboardSectionLayout.KpiRow, DashboardSectionLoadStrategy.Eager, 0),
        Section(DashboardSectionId.InvestmentsAnalytics, DashboardModule.Investments,
            "Investment Analytics", null, DashboardSectionLayout.GroupedFields,
            DashboardSectionLoadStrategy.Lazy, 10),
        Section(DashboardSectionId.InvestmentsCapitalSummary, DashboardModule.Investments,
            "Capital Summary", null, DashboardSectionLayout.Fields,
            DashboardSectionLoadStrategy.Lazy, 20),
        Section(DashboardSectionId.InvestmentsRiskCompliance, DashboardModule.Investments,
            "Risk & Compliance", null, DashboardSectionLayout.Fields,
            DashboardSectionLoadStrategy.Lazy, 30),
        Section(DashboardSectionId.InvestmentsList, DashboardModule.Investments,
            "Investment List", null, DashboardSectionLayout.Table,
            DashboardSectionLoadStrategy.Lazy, 40, dataRoute: "/api/Funds"),
        Section(DashboardSectionId.InvestmentsTransactions, DashboardModule.Investments,
            "Transactions", "Recent fund-level capital activity", DashboardSectionLayout.Table,
            DashboardSectionLoadStrategy.Lazy, 50,
            dataRoute: "/api/dashboard/modules/investments/transactions", isEnabled: false),

        // Assets module
        Section(DashboardSectionId.AssetsKpiSummary, DashboardModule.Assets,
            "Summary", null, DashboardSectionLayout.KpiRow, DashboardSectionLoadStrategy.Eager, 0),
        Section(DashboardSectionId.AssetsAnalytics, DashboardModule.Assets,
            "Asset Analytics", null, DashboardSectionLayout.GroupedFields,
            DashboardSectionLoadStrategy.Lazy, 10),
        Section(DashboardSectionId.AssetsPortfolioSummary, DashboardModule.Assets,
            "Portfolio Summary", null, DashboardSectionLayout.Fields,
            DashboardSectionLoadStrategy.Lazy, 20),
        Section(DashboardSectionId.AssetsList, DashboardModule.Assets,
            "Asset List", null, DashboardSectionLayout.Table,
            DashboardSectionLoadStrategy.Lazy, 30, dataRoute: "/api/Assets"),
        Section(DashboardSectionId.AssetsTransactions, DashboardModule.Assets,
            "Transactions", "Recent asset-level activity", DashboardSectionLayout.Table,
            DashboardSectionLoadStrategy.Lazy, 40,
            dataRoute: "/api/dashboard/modules/assets/transactions", isEnabled: false)
    ];

    public static IReadOnlyList<DashboardSectionDefinitionDto> GetByModule(DashboardModule module) =>
        Definitions
            .Where(d => d.Module == module && d.IsEnabled)
            .OrderBy(d => d.SortOrder)
            .Select(ToDto)
            .ToList();

    public static SectionDefinition? TryGet(DashboardSectionId sectionId) =>
        Definitions.FirstOrDefault(d => d.Id == sectionId && d.IsEnabled);

    private static SectionDefinition Section(
        DashboardSectionId id,
        DashboardModule module,
        string title,
        string? subtitle,
        DashboardSectionLayout layout,
        DashboardSectionLoadStrategy loadStrategy,
        int sortOrder,
        string? dataRoute = null,
        bool isEnabled = true) =>
        new(id, module, title, subtitle, layout, loadStrategy, sortOrder, dataRoute, isEnabled);

    private static DashboardSectionDefinitionDto ToDto(SectionDefinition definition) =>
        new()
        {
            Id = DashboardSectionIds.ToApiString(definition.Id),
            Module = DashboardModules.ToApiString(definition.Module),
            Title = definition.Title,
            Subtitle = definition.Subtitle,
            Layout = ToLayoutApiString(definition.Layout),
            LoadStrategy = ToLoadStrategyApiString(definition.LoadStrategy),
            SortOrder = definition.SortOrder,
            IsEnabled = definition.IsEnabled,
            DataRoute = definition.DataRoute
        };

    internal static string ToLayoutApiString(DashboardSectionLayout layout) =>
        layout switch
        {
            DashboardSectionLayout.KpiRow => "kpiRow",
            DashboardSectionLayout.Fields => "fields",
            DashboardSectionLayout.GroupedFields => "groupedFields",
            DashboardSectionLayout.Table => "table",
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unsupported section layout.")
        };

    internal static string ToLoadStrategyApiString(DashboardSectionLoadStrategy strategy) =>
        strategy switch
        {
            DashboardSectionLoadStrategy.Eager => "eager",
            DashboardSectionLoadStrategy.Lazy => "lazy",
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unsupported load strategy.")
        };

    internal sealed record SectionDefinition(
        DashboardSectionId Id,
        DashboardModule Module,
        string Title,
        string? Subtitle,
        DashboardSectionLayout Layout,
        DashboardSectionLoadStrategy LoadStrategy,
        int SortOrder,
        string? DataRoute,
        bool IsEnabled);
}
