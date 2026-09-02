using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class FundPortalService
{
    public async Task<TransactionFilterOptionsDto> GetFundCapitalActivitiesFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period) =>
        await GetFundTransactionInvestorFiltersAsync(fundKey, view, period);

    public async Task<TransactionFilterOptionsDto> GetFundDistributionsFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period) =>
        await GetFundTransactionInvestorFiltersAsync(fundKey, view, period);

    public async Task<TransactionFilterOptionsDto> GetFundIrrFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period) =>
        await GetFundTransactionInvestorFiltersAsync(fundKey, view, period);

    public async Task<TransactionFilterOptionsDto> GetFundObligationsFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period) =>
        await GetFundTransactionInvestorFiltersAsync(fundKey, view, period);

    public async Task<TransactionFilterOptionsDto> GetFundNetAssetsFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period) =>
        await Task.FromResult(new TransactionFilterOptionsDto { Items = [] });

    private async Task<TransactionFilterOptionsDto> GetFundTransactionInvestorFiltersAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        var factTable = PortfolioFactTable(view);
        var sql = new StringBuilder();
        sql.Append(" select distinct ");
        sql.Append(" isnull(i.investor_name, '') as investor_name ");
        AppendFundPortfolioFrom(sql, factTable);
        AppendFundTransactionWhere(sql, view, period, applyInvestorNameFilter: false);
        sql.Append(" order by investor_name ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddFundTransactionParameters(command, fundKey, period, null, null);

        var items = new List<PortalFilterOptionDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetStringOrEmpty("investor_name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            items.Add(new PortalFilterOptionDto
            {
                Value = name,
                Label = name
            });
        }

        return new TransactionFilterOptionsDto { Items = items };
    }
}
