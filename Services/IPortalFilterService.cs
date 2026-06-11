using kingsightapi.Entities;

namespace kingsightapi.Services;

public interface IPortalFilterService
{
    Task<InvestorListFilterOptionsDto> GetInvestorListFilterOptionsAsync();
    Task<FundListFilterOptionsDto> GetFundListFilterOptionsAsync();
    Task<AssetListFilterOptionsDto> GetAssetListFilterOptionsAsync();
}
