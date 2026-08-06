namespace kingsightapi.Entities
{
    public sealed class ManagementSummaryRowDto
    {
        public int LoanAliasKey { get; init; }
        public string LoanAliasName { get; init; } = string.Empty;
        public int? Ranking { get; init; }
        public string InvestorAliasName { get; init; } = string.Empty;
        public int LoanCount { get; init; }
        public decimal? TotalExposure { get; init; }
        public decimal? SecurityValue { get; init; }
        public decimal? AvgLtv { get; init; }
        public string? DefaultStatus { get; init; }
        public DateTime? DefaultDate { get; init; }
    }

    public sealed class LoanDetailReportRowDto
    {
        public long LoanKey { get; init; }
        public string ParentLoanId { get; init; } = string.Empty;
        public string ChildLoanId { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string InvestorAliasName { get; init; } = string.Empty;
        public decimal? SecurityValue { get; init; }
        public decimal? Exposure { get; init; }
        public decimal? Ltv { get; init; }
    }

    public sealed class ManagementSummaryDashboardQuery
    {
        public DateOnly AsOfDate { get; init; }
        public DateOnly? DefaultDateFrom { get; init; }
        public DateOnly? DefaultDateTo { get; init; }
        public DateOnly? MaturityDateFrom { get; init; }
        public DateOnly? MaturityDateTo { get; init; }
        public string? Sponsor { get; init; }
        public IReadOnlyList<string>? RiskLevels { get; init; }
        public IReadOnlyList<string>? Statuses { get; init; }
        public IReadOnlyList<string>? InvestorAliases { get; init; }
        public IReadOnlyList<int>? LoanAliasIds { get; init; }
    }

    public sealed class LoanDetailReportQuery
    {
        public DateOnly AsOfDate { get; init; }
        public IReadOnlyList<string>? Statuses { get; init; }
    }

    public sealed class ManagementSummaryDashboardDto
    {
        public DateOnly AsOfDate { get; init; }
        public string ReportPeriodLabel { get; init; } = string.Empty;
        public ManagementSummaryKpisDto Kpis { get; init; } = new();
        public ManagementSummaryOutstandingInterestDto OutstandingInterest { get; init; } = new();
        public IReadOnlyList<LoanAliasSummaryRowDto> LoanAliasRows { get; init; } = [];
        public IReadOnlyList<ExposureAnalysisRowDto> ExposureAnalysisRows { get; init; } = [];
        public IReadOnlyList<CmhcWatchlistRowDto> WatchlistRows { get; init; } = [];
        /// <summary>Latest <c>report_date</c> from bronze <c>cmhc_default</c> (watchlist As At).</summary>
        public DateTime? WatchlistAsAt { get; init; }
        public ManagementSummaryFilterOptionsDto FilterOptions { get; init; } = new();
        public ManagementSummaryChartsPhase2Dto ChartsPhase2 { get; init; } = new();
    }

    public sealed class ManagementSummaryKpisDto
    {
        public int NumberOfLoans { get; init; }
        public decimal TotalOutstandingBalance { get; init; }
        public decimal? AverageLtv { get; init; }
        public decimal? PercentOfFundings { get; init; }
        public string? AverageLtvTrendLabel { get; init; }
        public decimal? MaxLtv { get; init; }
    }

    public sealed class ManagementSummaryOutstandingInterestDto
    {
        public decimal InterestDisbursed { get; init; }
        public decimal InterestNotDisbursed { get; init; }
        public decimal TotalOutstandingInterest { get; init; }
        public decimal TotalLateInterest { get; init; }
    }

    public sealed class LoanAliasSummaryRowDto
    {
        public int LoanAliasKey { get; init; }
        public string LoanAlias { get; init; } = string.Empty;
        public string? Sponsor { get; init; }
        public DateTime? DefaultDate { get; init; }
        public DateTime? MaturityDate { get; init; }
        public string? InterestStatus { get; init; }
        public string? Units { get; init; }
        public string? Exit { get; init; }
        public decimal? Security { get; init; }
        public decimal Principal { get; init; }
        public decimal OsInt { get; init; }
        public decimal Accrued { get; init; }
        public decimal LateInt { get; init; }
        public decimal TaxIns { get; init; }
        public decimal IntAdv { get; init; }
        public decimal Other { get; init; }
        public decimal TotalExposure { get; init; }
        public decimal? Ltv { get; init; }
        public string Risk { get; init; } = "LOW";
    }

    public sealed class ExposureAnalysisRowDto
    {
        public int LoanAliasKey { get; init; }
        public string LoanAlias { get; init; } = string.Empty;
        public string Sponsor { get; init; } = string.Empty;
        public decimal ExternalBalance { get; init; }
        public decimal SmfBalance { get; init; }
        public decimal MlpBalance { get; init; }
        public decimal TotalKsExposure { get; init; }
        public decimal SubordinateExposure { get; init; }
        public decimal? Ltv { get; init; }
    }

    public sealed class CmhcWatchlistRowDto
    {
        public string LoanId { get; init; } = string.Empty;
        public string Investor { get; init; } = string.Empty;
        public string Sponsor { get; init; } = string.Empty;
        public string Property { get; init; } = string.Empty;
        public string? Missed { get; init; }
        public decimal? Principal { get; init; }
        public decimal? OsInterest { get; init; }
        public string? TaxArrears { get; init; }
        public string? Ltv { get; init; }
        public string? Dscr { get; init; }
        public string? Issue { get; init; }
        public string? StatusUpdate { get; init; }
        public string? Conclusion { get; init; }
        public string Status { get; init; } = "NO CONCERNS";
        public DateTime? ReportDate { get; init; }
    }

    public sealed class ManagementSummaryFilterOptionsDto
    {
        public IReadOnlyList<string> Sponsors { get; init; } = [];
        public IReadOnlyList<string> InvestorAliases { get; init; } = [];
        public IReadOnlyList<string> RiskLevels { get; init; } =
            ["ALL", "HIGH", "ELEVATED", "MODERATE", "LOW"];
        public IReadOnlyList<string> Statuses { get; init; } =
            ["Unfunded", "Funded", "Default", "Repaid", "All"];
    }

    public sealed class ManagementSummaryChartsPhase2Dto
    {
        public IReadOnlyList<ChartSliceDto> LtvRiskDistribution { get; init; } = [];
        public IReadOnlyList<ChartSliceDto> Top5Exposures { get; init; } = [];
        public IReadOnlyList<ChartSliceDto> ExposureBreakdown { get; init; } = [];
        public IReadOnlyList<ChartSliceDto> ExposureAnalysis { get; init; } = [];
        public IReadOnlyList<ChartSliceDto> InvestorSummary { get; init; } = [];
        public IReadOnlyList<ChartSliceDto> SponsorSummary { get; init; } = [];
    }

    public sealed class LoanDetailReportDashboardDto
    {
        public string LoanAlias { get; init; } = string.Empty;
        public LoanDetailReportHeaderDto Header { get; init; } = new();
        public LoanDetailReportDetailsDto ReportDetails { get; init; } = new();
        public LoanDetailReportKeyDatesDto KeyDates { get; init; } = new();
        public LoanDetailReportPropertyStatsDto PropertyStats { get; init; } = new();
        public LoanDetailReportInterestSummaryDto InterestSummary { get; init; } = new();
        public LoanDetailReportInterestReserveDto InterestReserve { get; init; } = new();
        public IReadOnlyList<LoanPortfolioDetailRowDto> PortfolioRows { get; init; } = [];
        public LoanPortfolioDetailTotalsDto? PortfolioTotals { get; init; }
        public IReadOnlyList<ChartSliceDto> ExposureByInvestor { get; init; } = [];
        public IReadOnlyList<ChartSliceDto> ExposureComposition { get; init; } = [];
        public IReadOnlyList<ChartSliceDto> InvestorBreakdown { get; init; } = [];
        public DateTime? TaxArrearsAsAt { get; init; }
        public IReadOnlyList<TaxArrearsByYearDto> TaxArrearsByYear { get; init; } = [];
    }

    public sealed class LoanDetailReportHeaderDto
    {
        public decimal? SecurityValue { get; init; }
        public decimal? OverallLtv { get; init; }
        public decimal? EquityCushion { get; init; }
        public string? Units { get; init; }
    }

    public sealed class LoanDetailReportDetailsDto
    {
        public string? MainLoanId { get; init; }
        public string? LoanType { get; init; }
        public string? InvestorAlias { get; init; }
        public int? Ranking { get; init; }
    }

    public sealed class LoanDetailReportKeyDatesDto
    {
        public DateTime? DateOfDefault { get; init; }
        public int? DaysInDefault { get; init; }
        public DateTime? MaturityDate { get; init; }
        public DateOnly AsOfDate { get; init; }
    }

    public sealed class LoanDetailReportPropertyStatsDto
    {
        public decimal? ValuePerUnit { get; init; }
        public string? RiskStatus { get; init; }
        public string? PropertyType { get; init; }
        public string? Location { get; init; }
    }

    public sealed class LoanDetailReportInterestSummaryDto
    {
        public decimal InterestDisbursed { get; init; }
        public decimal InterestNotDisbursed { get; init; }
        public decimal TotalOutstandingInterest { get; init; }
        public int? MonthsInArrears { get; init; }
    }

    public sealed class LoanDetailReportInterestReserveDto
    {
        public decimal? CurrentInterestReserve { get; init; }
        public decimal? CurrentInterestReserveBalance { get; init; }
        public decimal? MonthsCoveredByReserve { get; init; }
    }

    public sealed class LoanPortfolioDetailRowDto
    {
        public string LoanId { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Investor { get; init; } = string.Empty;
        public int? Rank { get; init; }
        public decimal? Rate { get; init; }
        public decimal Principal { get; init; }
        public decimal DefInterest { get; init; }
        public decimal AccruedInt { get; init; }
        public decimal LateInt { get; init; }
        public decimal IntAdj { get; init; }
        public decimal TaxArrears { get; init; }
        public decimal OtherCosts { get; init; }
        public decimal TotalExposure { get; init; }
        public decimal? Ltv { get; init; }
        public int? MonthsInArrears { get; init; }
        public int? TimesNsfd { get; init; }
    }

    public sealed class LoanPortfolioDetailTotalsDto
    {
        public decimal Principal { get; init; }
        public decimal DefInterest { get; init; }
        public decimal AccruedInt { get; init; }
        public decimal LateInt { get; init; }
        public decimal IntAdj { get; init; }
        public decimal TaxArrears { get; init; }
        public decimal OtherCosts { get; init; }
        public decimal TotalExposure { get; init; }
    }

    public sealed class ChartSliceDto
    {
        public string Label { get; init; } = string.Empty;
        public decimal Value { get; init; }
        public decimal? SharePercent { get; init; }
        public int? Count { get; init; }
        public decimal? AverageLtv { get; init; }
    }

    public sealed class TaxArrearsByYearDto
    {
        public int Year { get; init; }
        public decimal TaxArrears { get; init; }
    }
}
