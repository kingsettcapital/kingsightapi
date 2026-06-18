using kingsightapi.Entities;

namespace kingsightapi.Services;

public interface IFundPortalService
{
    Task<PortalListPageResult<FundListItemDto, FundListSummaryDto>> GetFundsAsync(
        string? search,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? fundType,
        string? strategy,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize);
    Task<FundProfileDto?> GetFundByKeyAsync(int fundKey);
    Task<PagedResult<FundInvestorDto>> GetFundInvestorsAsync(int fundKey, string? search, int page, int pageSize);
    Task<PagedResult<FundAssetDto>> GetFundAssetsAsync(int fundKey, int page, int pageSize);
    Task<PagedResult<FundPeriodDto>> GetFundPeriodsAsync(
        int fundKey,
        TimeGranularity view,
        FundMetricSource source,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetFundCommitmentsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetFundNavAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetFundUnfundedCommitmentsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetFundInvestmentsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundDistributionGroupDto>> GetFundDistributionsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);

    /// <summary>Capital Activities table (one row per investor) for the fund portfolio screen. Searchable by investor code or name; sortable.</summary>
    Task<PagedResult<FundInvestorCapitalActivitiesDto>> GetFundCapitalActivitiesAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize);

    /// <summary>Distributions table (one row per investor) for the fund portfolio screen. Searchable by investor code or name; sortable.</summary>
    Task<PagedResult<FundInvestorDistributionsDto>> GetFundDistributionsSummaryAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize);

    /// <summary>IRR table (one row per investor) for the fund portfolio screen. Searchable by investor code or name; sortable.</summary>
    Task<PagedResult<FundInvestorIrrDto>> GetFundIrrAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize);

    /// <summary>Capital obligations table (quarterly only; unpivoted Commitment/Unfunded/Reserve/Release rows).</summary>
    Task<PagedResult<FundInvestorObligationDto>> GetFundCapitalObligationsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize);

    Task<TransactionFilterOptionsDto> GetFundCapitalActivitiesFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period);

    Task<TransactionFilterOptionsDto> GetFundDistributionsFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period);

    Task<TransactionFilterOptionsDto> GetFundIrrFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period);

    Task<TransactionFilterOptionsDto> GetFundObligationsFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period);

    /// <summary>Net assets table (quarterly only; unpivoted IRR horizon rows).</summary>
    Task<PagedResult<FundInvestorNetAssetsDto>> GetFundNetAssetsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize);

    Task<TransactionFilterOptionsDto> GetFundNetAssetsFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period);
}
