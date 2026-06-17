using kingsightapi.Entities;

namespace kingsightapi.Services;

public interface IInvestorPortalService
{
    Task<PortalListPageResult<InvestorListItemDto, InvestorListSummaryDto>> GetInvestorsAsync(
        string? search,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? investorType,
        string? relationship,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize);
    Task<InvestorDetailDto?> GetInvestorByKeyAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period);
    Task<PagedResult<InvestorInvestmentDto>> GetInvestorFundsAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundPeriodDto>> GetInvestorPeriodsAsync(
        long investorKey,
        TimeGranularity view,
        FundMetricSource source,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetInvestorCommitmentsAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetInvestorUnfundedCommitmentsAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetInvestorInvestmentActivityAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundDistributionGroupDto>> GetInvestorDistributionsAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetInvestorNavAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);

    /// <summary>Capital Activities table (one row per fund) for the investor portfolio screen. Searchable by fund code or name; sortable.</summary>
    Task<PagedResult<InvestorFundCapitalActivitiesDto>> GetInvestorCapitalActivitiesAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize);

    /// <summary>Distributions table (one row per fund) for the investor portfolio screen. Searchable by fund code or name; sortable.</summary>
    Task<PagedResult<InvestorFundDistributionsDto>> GetInvestorDistributionsSummaryAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize);

    /// <summary>IRR table (one row per fund) for the investor portfolio screen. Searchable by fund code or name; sortable.</summary>
    Task<PagedResult<InvestorFundIrrDto>> GetInvestorIrrAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize);

    Task<PagedResult<InvestorFundExposureDto>> GetInvestorFundExposureAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);

    Task<PagedResult<InvestorUnderlyingAssetDto>> GetInvestorAssetsAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
}
