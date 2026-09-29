using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class InvestorPortalService
{
    public async Task<InvestorFundHoldingsResultDto> GetInvestorFundHoldingsAsync(long investorKey)
    {
        try
        {
            return await GetInvestorFundHoldingsInternalAsync(investorKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get fund holdings for investor {InvestorKey} cancelled", investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fund holdings for investor {InvestorKey}", investorKey);
            throw;
        }
    }

    public async Task<TransactionFilterOptionsDto> GetInvestorCapitalActivitiesFiltersAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period) =>
        await GetInvestorTransactionFundFiltersAsync(investorKey, view, period);

    public async Task<TransactionFilterOptionsDto> GetInvestorDistributionsFiltersAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period) =>
        await GetInvestorTransactionFundFiltersAsync(investorKey, view, period);

    public async Task<TransactionFilterOptionsDto> GetInvestorIrrFiltersAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period) =>
        await GetInvestorTransactionFundFiltersAsync(investorKey, view, period);

    public async Task<TransactionFilterOptionsDto> GetInvestorObligationsFiltersAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period) =>
        await GetInvestorTransactionFundFiltersAsync(investorKey, view, period);

    public async Task<TransactionFilterOptionsDto> GetInvestorNetAssetsFiltersAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period) =>
        await GetInvestorNavFundFiltersAsync(investorKey, period);

    private async Task<TransactionFilterOptionsDto> GetInvestorNavFundFiltersAsync(
        long investorKey,
        FundPeriodFilter? period)
    {
        var sql = new StringBuilder();
        sql.Append(" select distinct ");
        sql.Append(" isnull(f.fund_code, '') as fund_code, ");
        sql.Append(" isnull(f.fund_name, '') as fund_name ");
        PortalPortfolioTransactionSql.AppendInvestorNavFrom(sql);
        sql.Append(" where 1=1 ");
        PortalPortfolioTransactionSql.AppendUnitizedFundFilter(sql);
        PortalPortfolioTransactionSql.AppendNavQuarterlyPeriodFilter(sql, period);
        sql.Append(" order by fund_code ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddInvestorTransactionParameters(command, investorKey, period, null, null);

        var items = new List<PortalFilterOptionDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetStringOrEmpty("fund_code");
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            items.Add(new PortalFilterOptionDto
            {
                Value = code,
                Label = reader.GetStringOrEmpty("fund_name")
            });
        }

        return new TransactionFilterOptionsDto { Items = items };
    }

    private async Task<TransactionFilterOptionsDto> GetInvestorTransactionFundFiltersAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        var factTable = PortfolioFactTable(view);
        var sql = new StringBuilder();
        sql.Append(" select distinct ");
        sql.Append(" isnull(f.fund_code, '') as fund_code, ");
        sql.Append(" isnull(f.fund_name, '') as fund_name ");
        AppendInvestorPortfolioFrom(sql, factTable);
        AppendInvestorTransactionWhere(sql, view, period, applyFundCodeFilter: false);
        sql.Append(" order by fund_code ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddInvestorTransactionParameters(command, investorKey, period, null, null);

        var items = new List<PortalFilterOptionDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetStringOrEmpty("fund_code");
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            items.Add(new PortalFilterOptionDto
            {
                Value = code,
                Label = reader.GetStringOrEmpty("fund_name")
            });
        }

        return new TransactionFilterOptionsDto { Items = items };
    }

    private async Task<InvestorFundHoldingsResultDto> GetInvestorFundHoldingsInternalAsync(long investorKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" max(p.date_key) as date_key, ");
        sql.Append(" f.fund_key, ");
        sql.Append(" max(isnull(f.fund_name, '')) as fund_name, ");
        sql.Append(" min(since_dates.since_date) as since_date, ");
        sql.Append(" sum(isnull(p.commitment_amount, 0)) as commitment, ");
        sql.Append(" sum(isnull(p.unfunded_amount, 0)) as unfunded, ");
        //PortalPortfolioListSql.AppendUnfundedAmountExpression(sql, "p");
        sql.Append(" sum(isnull(p.reserved_amount, 0)) as reserved, ");
        sql.Append(" sum(isnull(p.net_invested_capital_amount, 0)) as net_invested, ");
        sql.Append(" sum(isnull(p.preferred_return_amount, 0)) ");
        sql.Append(" + sum(isnull(p.sales_gain_amount, 0)) ");
        sql.Append(" + sum(isnull(p.excess_cash_amount, 0)) as distributed_amount ");
        sql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} p ");
        sql.Append($" inner join {WarehouseTables.DimFund} f on f.fund_key = p.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append($" inner join {WarehouseTables.DimInvestor} i on i.investor_key = p.investor_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
        sql.Append(" left join ( ");
        sql.Append(" select fi.fund_key, ");
        sql.Append(" min(try_convert(date, cast(fi.calculation_date_key as varchar(8)), 112)) as since_date ");
        sql.Append($" from {WarehouseTables.FactInvestment} fi ");
        sql.Append(" where fi.investor_key = @investorKey ");
        sql.Append(" group by fi.fund_key ");
        sql.Append(" ) as since_dates on since_dates.fund_key = f.fund_key ");
        sql.Append(" where p.investor_key = @investorKey ");
        sql.Append(" and p.date_key = ( ");
        sql.Append($" select max(date_key) from {WarehouseTables.FactInvestorPortfolioLtd} ");
        sql.Append(" where investor_key = @investorKey ");
        sql.Append(" ) ");
        sql.Append(" group by f.fund_key ");
        sql.Append(" order by fund_name ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@investorKey", investorKey);

        int? dateKey = null;
        var items = new List<InvestorFundHoldingDto>();

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                dateKey ??= reader.GetNullableInt32("date_key");

                items.Add(new InvestorFundHoldingDto
                {
                    FundKey = reader.GetInt32OrDefault("fund_key"),
                    FundName = reader.GetStringOrEmpty("fund_name"),
                    Since = reader.GetNullableDateTime("since_date"),
                    Commitment = reader.GetDecimalOrDefault("commitment"),
                    Unfunded = reader.GetDecimalOrDefault("unfunded"),
                    NetInvested = reader.GetDecimalOrDefault("net_invested"),
                    Reserved = reader.GetDecimalOrDefault("reserved"),
                    Distributed = reader.GetDecimalOrDefault("distributed_amount")
                });
            }
        }

        return new InvestorFundHoldingsResultDto
        {
            DateKey = dateKey,
            Items = items
        };
    }

    private static string BuildContactDisplay(
        string firstName,
        string lastName,
        string email)
    {
        var name = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return email;
        }

        return string.IsNullOrWhiteSpace(email) ? name : $"{name} | {email}";
    }

    private async Task<List<InvestorProfileFundDto>> LoadInvestorProfileFundsAsync(
        SqlConnection connection,
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        var portfolioTable = PortalPortfolioListSql.PortfolioTable(view);
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" f.fund_key, ");
        sql.Append(" max(isnull(f.fund_code, '')) as fund_code, ");
        sql.Append(" max(isnull(f.fund_name, '')) as fund_name ");
        sql.Append($" from {portfolioTable} p ");
        sql.Append($" inner join {WarehouseTables.DimFund} f on f.fund_key = p.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" where p.investor_key = @investorKey ");
        PortalPortfolioListSql.AppendQuarterlyPeriodFilter(sql, view, period);
        sql.Append(" group by f.fund_key ");
        sql.Append(" order by fund_code ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@investorKey", investorKey);
        PortalPortfolioListSql.AddPeriodParameter(command, period);

        var funds = new List<InvestorProfileFundDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            funds.Add(new InvestorProfileFundDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name")
            });
        }

        return funds;
    }

    private async Task<decimal> LoadCapitalDeployedAsync(
        SqlConnection connection,
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        var portfolioTable = PortalPortfolioListSql.PortfolioTable(view);
        var sql = new StringBuilder();
        sql.Append(" select sum(isnull(p.capital_called_amount, 0)) as capital_deployed ");
        sql.Append($" from {portfolioTable} p ");
        sql.Append($" inner join {WarehouseTables.DimInvestor} i on i.investor_key = p.investor_key ");
        sql.Append(" where p.investor_key = @investorKey and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
        PortalPortfolioListSql.AppendQuarterlyPeriodFilter(sql, view, period);

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@investorKey", investorKey);
        PortalPortfolioListSql.AddPeriodParameter(command, period);

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? 0m : Convert.ToDecimal(result);
    }
}
