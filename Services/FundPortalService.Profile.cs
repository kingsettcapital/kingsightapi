using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class FundPortalService
{
    private async Task<FundProfileDto?> GetFundByKeyInternalAsync(int fundKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" f.fund_key, ");
        sql.Append(" isnull(f.fund_code, '') as fund_code, ");
        sql.Append(" isnull(f.fund_name, '') as fund_name, ");
        sql.Append(" isnull(f.fund_type_name, 'Fund') as fund_type_name, ");
        sql.Append(" isnull(f.fund_strategy_name, '') as fund_strategy_name, ");
        sql.Append(" case when isnull(f.is_active, 0) = 1 then 'Active' else 'Inactive' end as fund_status, ");
        sql.Append(" f.fund_start_date, ");
        sql.Append(" isnull(f.is_sidecar, 0) as is_sidecar, ");
        sql.Append(" isnull(port.commitment, 0) as total_commitment, ");
        sql.Append(" isnull(port.called, 0) as capital_deployed, ");
        sql.Append(" isnull(port.net_invested_capital, 0) as net_invested_capital, ");
        sql.Append(" isnull(port.net_distributed, 0) as net_distributed, ");
        sql.Append(" isnull(port.reserved_uncalled, 0) as reserved_uncalled, ");
        sql.Append(" port.released_capital as released_capital, ");
        sql.Append(" isnull(assets.assets_count, 0) as asset_count, ");
        sql.Append(" isnull(inv.investors_count, 0) as investor_count ");
        sql.Append($" from {WarehouseTables.DimFund} f ");
        sql.Append(" outer apply ( ");
        sql.Append(" select ");
        sql.Append(" sum(isnull(commitment_amount, 0)) as commitment, ");
        sql.Append(" sum(isnull(capital_called_amount, 0)) as called, ");
        sql.Append(" sum(isnull(net_invested_capital_amount, 0)) as net_invested_capital, ");
        sql.Append(" sum(isnull(preferred_return_amount, 0)) ");
        sql.Append(" + sum(isnull(sales_gain_amount, 0)) ");
        sql.Append(" + sum(isnull(excess_cash_amount, 0)) as net_distributed, ");
        sql.Append(" sum(isnull(reserved_amount, 0)) as reserved_uncalled, ");
        sql.Append(" sum(isnull(released_capital_amount, 0)) as released_capital ");
        sql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} ");
        sql.Append(" where fund_key = f.fund_key ");
        sql.Append(" ) port ");
        sql.Append(" outer apply ( ");
        sql.Append(" select count(*) as assets_count ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        WarehouseSql.AppendPropertyBelongsToFundFilter(sql, "p", "f");
        WarehouseSql.AppendPropertyFundLevel000Filter(sql, "p");
        sql.Append(" ) assets ");
        sql.Append(" outer apply ( ");
        sql.Append(" select count(*) as investors_count ");
        sql.Append(" from ( ");
        sql.Append($" select investor_key from {WarehouseTables.FactInvestorPortfolioLtd} where fund_key = f.fund_key ");
        sql.Append(" ) invkeys ");
        sql.Append(" ) inv ");
        sql.Append(" where f.fund_key = @fundKey ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@fundKey", fundKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var fundKeyValue = reader.GetInt32OrDefault("fund_key");
        var fundCode = reader.GetStringOrEmpty("fund_code");
        var fundName = reader.GetStringOrEmpty("fund_name");
        var fundType = reader.GetStringOrEmpty("fund_type_name");
        var strategy = reader.GetStringOrEmpty("fund_strategy_name");
        var status = reader.GetStringOrEmpty("fund_status");
        var startDate = reader.GetNullableDateTime("fund_start_date");
        var isSidecar = reader.GetInt32OrDefault("is_sidecar") == 1;
        var totalCommitment = reader.GetDecimalOrDefault("total_commitment");
        var capitalDeployed = reader.GetDecimalOrDefault("capital_deployed");
        var netInvestedCapital = reader.GetDecimalOrDefault("net_invested_capital");
        var netDistributed = reader.GetDecimalOrDefault("net_distributed");
        var reservedUncalled = reader.GetDecimalOrDefault("reserved_uncalled");
        var releasedCapital = reader.GetNullableDecimal("released_capital");
        var assetCount = reader.GetInt32OrDefault("asset_count");
        var investorCountFromSql = reader.GetInt32OrDefault("investor_count");

        await reader.DisposeAsync();

        var investors = await LoadFundProfileInvestorsAsync(connection, fundKey);

        return new FundProfileDto
        {
            FundKey = fundKeyValue,
            FundCode = fundCode,
            FundName = fundName,
            FundType = fundType,
            Strategy = strategy,
            Status = status,
            StartDate = startDate,
            IsSidecar = isSidecar,
            TotalCommitment = totalCommitment,
            CapitalDeployed = capitalDeployed,
            NetInvestedCapital = netInvestedCapital,
            NetDistributed = netDistributed,
            ReservedUncalled = reservedUncalled,
            ReleasedCapital = releasedCapital,
            AssetCount = assetCount,
            InvestorCount = investors.Count > 0 ? investors.Count : investorCountFromSql,
            Investors = investors
        };
    }

    private async Task<List<FundProfileInvestorDto>> LoadFundProfileInvestorsAsync(SqlConnection connection, int fundKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" i.investor_key, ");
        sql.Append(" max(isnull(i.investor_name, '')) as investor_name ");
        sql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} p ");
        sql.Append($" inner join {WarehouseTables.DimInvestor} i on i.investor_key = p.investor_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
        sql.Append(" where p.fund_key = @fundKey ");
        sql.Append(" group by i.investor_key ");
        sql.Append(" order by investor_name ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@fundKey", fundKey);

        var investors = new List<FundProfileInvestorDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            investors.Add(new FundProfileInvestorDto
            {
                InvestorKey = reader.GetInt64OrDefault("investor_key"),
                InvestorName = reader.GetStringOrEmpty("investor_name")
            });
        }

        return investors;
    }
}
