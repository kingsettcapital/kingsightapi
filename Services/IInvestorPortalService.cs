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
    Task<InvestorDetailDto?> GetInvestorByKeyAsync(long investorKey);
    Task<PagedResult<InvestorInvestmentDto>> GetInvestorFundsAsync(long investorKey, int page, int pageSize);
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
}
