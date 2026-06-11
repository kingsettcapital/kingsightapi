using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class DashboardService : IDashboardService
{
    private readonly string _connectionString;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IConfiguration configuration, ILogger<DashboardService> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        _logger = logger;
    }

    public IReadOnlyList<DashboardSectionDefinitionDto> GetModuleSections(DashboardModule module) =>
        DashboardSectionRegistry.GetByModule(module);

    public async Task<DashboardSectionDataDto?> GetSectionDataAsync(DashboardSectionId sectionId, TimeGranularity view)
    {
        var definition = DashboardSectionRegistry.TryGet(sectionId);
        if (definition is null)
        {
            return null;
        }

        if (definition.Layout == DashboardSectionLayout.Table)
        {
            throw new InvalidOperationException(
                $"Section '{DashboardSectionIds.ToApiString(sectionId)}' is a table section. " +
                $"Use data route '{definition.DataRoute}' for row data.");
        }

        try
        {
            return sectionId switch
            {
                DashboardSectionId.InvestorsKpiSummary =>
                    await BuildInvestorsKpiSummaryAsync(view, definition),
                DashboardSectionId.InvestorsAnalyticsBenchmarking =>
                    await BuildInvestorsAnalyticsBenchmarkingAsync(view, definition),
                DashboardSectionId.InvestorsCapitalAccountSummary =>
                    await BuildInvestorsCapitalAccountSummaryAsync(view, definition),
                DashboardSectionId.InvestorsRiskCompliance =>
                    BuildPlaceholderFields(definition, view, "riskCompliance"),
                DashboardSectionId.InvestorsReportingCommunications =>
                    BuildPlaceholderFields(definition, view, "reportingCommunications"),
                DashboardSectionId.InvestorsPortalAccess =>
                    BuildPlaceholderFields(definition, view, "portalAccess"),

                DashboardSectionId.InvestmentsKpiSummary =>
                    await BuildInvestmentsKpiSummaryAsync(view, definition),
                DashboardSectionId.InvestmentsAnalytics =>
                    BuildPlaceholderGroupedFields(definition, view, "By Fund Type", "By Strategy"),
                DashboardSectionId.InvestmentsCapitalSummary =>
                    BuildPlaceholderFields(definition, view, "capitalSummary"),
                DashboardSectionId.InvestmentsRiskCompliance =>
                    BuildPlaceholderFields(definition, view, "riskCompliance"),

                DashboardSectionId.AssetsKpiSummary =>
                    await BuildAssetsKpiSummaryAsync(view, definition),
                DashboardSectionId.AssetsAnalytics =>
                    BuildPlaceholderGroupedFields(definition, view, "By Asset Type", "By Geography"),
                DashboardSectionId.AssetsPortfolioSummary =>
                    BuildPlaceholderFields(definition, view, "portfolioSummary"),

                _ => null
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Get dashboard section {SectionId} ({View}) cancelled",
                DashboardSectionIds.ToApiString(sectionId),
                TimeGranularities.ToApiString(view));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving dashboard section {SectionId} ({View})",
                DashboardSectionIds.ToApiString(sectionId),
                TimeGranularities.ToApiString(view));
            throw;
        }
    }

    public async Task<PagedResult<DashboardTransactionDto>> GetInvestorTransactionsAsync(
        TimeGranularity view,
        int page,
        int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => await GetInvestorTransactionsLtdAsync(page, pageSize),
                TimeGranularity.Quarterly => await GetInvestorTransactionsQuarterlyAsync(page, pageSize),
                TimeGranularity.Daily => await GetInvestorTransactionsDailyAsync(page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investor transactions ({View}) cancelled", view);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investor transactions ({View})", view);
            throw;
        }
    }

    private static DashboardSectionDataDto BuildPlaceholderFields(
        DashboardSectionRegistry.SectionDefinition definition,
        TimeGranularity view,
        string keyPrefix) =>
        BuildSectionData(
            definition,
            view,
            fields:
            [
                DisplayFieldBuilder.ToDynamicField(
                    $"{keyPrefix}Status",
                    DisplayFieldBuilder.Text("Pending configuration"))
            ]);

    private static DashboardSectionDataDto BuildPlaceholderGroupedFields(
        DashboardSectionRegistry.SectionDefinition definition,
        TimeGranularity view,
        params string[] groupTitles) =>
        BuildSectionData(
            definition,
            view,
            groups: groupTitles
                .Select(title => new DashboardSectionGroupDto
                {
                    Title = title,
                    Fields =
                    [
                        DisplayFieldBuilder.ToDynamicField(
                            "status",
                            DisplayFieldBuilder.Text("Pending configuration"))
                    ]
                })
                .ToList());

    private static DashboardSectionDataDto BuildSectionData(
        DashboardSectionRegistry.SectionDefinition definition,
        TimeGranularity view,
        IReadOnlyList<DashboardKpiCardDto>? kpis = null,
        IReadOnlyList<DynamicFieldDto>? fields = null,
        IReadOnlyList<DashboardSectionGroupDto>? groups = null) =>
        new()
        {
            SectionId = DashboardSectionIds.ToApiString(definition.Id),
            Title = definition.Title,
            Layout = DashboardSectionRegistry.ToLayoutApiString(definition.Layout),
            View = TimeGranularities.ToApiString(view),
            Kpis = kpis,
            Fields = fields,
            Groups = groups
        };
}
