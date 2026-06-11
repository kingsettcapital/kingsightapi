using kingsightapi.Entities;

namespace kingsightapi.Services;

public interface IDashboardService
{
    IReadOnlyList<DashboardSectionDefinitionDto> GetModuleSections(DashboardModule module);

    Task<DashboardSectionDataDto?> GetSectionDataAsync(DashboardSectionId sectionId, TimeGranularity view);

    Task<PagedResult<DashboardTransactionDto>> GetInvestorTransactionsAsync(
        TimeGranularity view,
        int page,
        int pageSize);
}
