namespace kingsightapi.Entities;

/// <summary>
/// Stable dashboard section identifiers. Serialized as kebab-case slugs in API routes
/// (e.g. <c>investors-analytics-benchmarking</c>).
/// </summary>
public enum DashboardSectionId
{
    InvestorsKpiSummary,
    InvestorsAnalyticsBenchmarking,
    InvestorsCapitalAccountSummary,
    InvestorsRiskCompliance,
    InvestorsReportingCommunications,
    InvestorsPortalAccess,
    InvestorsList,
    InvestorsTransactions,

    InvestmentsKpiSummary,
    InvestmentsAnalytics,
    InvestmentsCapitalSummary,
    InvestmentsRiskCompliance,
    InvestmentsList,
    InvestmentsTransactions,

    AssetsKpiSummary,
    AssetsAnalytics,
    AssetsPortfolioSummary,
    AssetsList,
    AssetsTransactions
}

/// <summary>Slug parsing and mapping for <see cref="DashboardSectionId"/>.</summary>
public static class DashboardSectionIds
{
    public static bool TryParseFromApi(string? slug, out DashboardSectionId sectionId)
    {
        sectionId = default;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        foreach (var candidate in Enum.GetValues<DashboardSectionId>())
        {
            if (string.Equals(ToApiString(candidate), slug.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                sectionId = candidate;
                return true;
            }
        }

        return false;
    }

    public static string ToApiString(DashboardSectionId sectionId) =>
        sectionId switch
        {
            DashboardSectionId.InvestorsKpiSummary => "investors-kpi-summary",
            DashboardSectionId.InvestorsAnalyticsBenchmarking => "investors-analytics-benchmarking",
            DashboardSectionId.InvestorsCapitalAccountSummary => "investors-capital-account-summary",
            DashboardSectionId.InvestorsRiskCompliance => "investors-risk-compliance",
            DashboardSectionId.InvestorsReportingCommunications => "investors-reporting-communications",
            DashboardSectionId.InvestorsPortalAccess => "investors-portal-access",
            DashboardSectionId.InvestorsList => "investors-list",
            DashboardSectionId.InvestorsTransactions => "investors-transactions",

            DashboardSectionId.InvestmentsKpiSummary => "investments-kpi-summary",
            DashboardSectionId.InvestmentsAnalytics => "investments-analytics",
            DashboardSectionId.InvestmentsCapitalSummary => "investments-capital-summary",
            DashboardSectionId.InvestmentsRiskCompliance => "investments-risk-compliance",
            DashboardSectionId.InvestmentsList => "investments-list",
            DashboardSectionId.InvestmentsTransactions => "investments-transactions",

            DashboardSectionId.AssetsKpiSummary => "assets-kpi-summary",
            DashboardSectionId.AssetsAnalytics => "assets-analytics",
            DashboardSectionId.AssetsPortfolioSummary => "assets-portfolio-summary",
            DashboardSectionId.AssetsList => "assets-list",
            DashboardSectionId.AssetsTransactions => "assets-transactions",

            _ => throw new ArgumentOutOfRangeException(nameof(sectionId), sectionId, "Unsupported dashboard section.")
        };
}
