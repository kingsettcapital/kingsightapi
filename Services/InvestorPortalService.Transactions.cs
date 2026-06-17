using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class InvestorPortalService
{
    public async Task<PagedResult<InvestorFundCapitalActivitiesDto>> GetInvestorCapitalActivitiesAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? sortBy,
        string? sortDir,
        int? fundKey,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseInvestorCapitalActivities(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var factTable = PortfolioFactTable(view);

        // Aggregate columns are the same for the count and page queries; only the projection differs.
        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" f.fund_key, ");
        pageSql.Append(" isnull(f.fund_code, '') as fund_code, ");
        pageSql.Append(" max(isnull(f.fund_name, '')) as fund_name, ");
        pageSql.Append(" sum(isnull(p.capital_called_amount, 0)) as called, ");
        pageSql.Append(" sum(isnull(p.investment_transferred_in_amount, 0)) as transfer_in, ");
        pageSql.Append(" sum(isnull(p.investment_transferred_out_amount, 0)) as transfer_out, ");
        pageSql.Append(" sum(isnull(p.redeemed_amount, 0)) as redemption ");
        AppendInvestorPortfolioFrom(pageSql, factTable);
        AppendInvestorTransactionWhere(pageSql, view, period, fundKey);
        pageSql.Append(" group by f.fund_key, f.fund_code ");
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorTransactionPageAsync(
            BuildInvestorTransactionCountSql(factTable, view, period, fundKey),
            pageSql,
            investorKey,
            period,
            search,
            fundKey,
            page,
            pageSize,
            static reader => new InvestorFundCapitalActivitiesDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                Called = reader.GetDecimalOrDefault("called"),
                TransferIn = reader.GetDecimalOrDefault("transfer_in"),
                TransferOut = reader.GetDecimalOrDefault("transfer_out"),
                Redemption = reader.GetDecimalOrDefault("redemption")
            });
    }

    public async Task<PagedResult<InvestorFundDistributionsDto>> GetInvestorDistributionsSummaryAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? sortBy,
        string? sortDir,
        int? fundKey,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseInvestorDistributions(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var factTable = PortfolioFactTable(view);

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" f.fund_key, ");
        pageSql.Append(" isnull(f.fund_code, '') as fund_code, ");
        pageSql.Append(" max(isnull(f.fund_name, '')) as fund_name, ");
        pageSql.Append(" sum(isnull(p.commitment_amount, 0)) as committed, ");
        PortalPortfolioListSql.AppendUnfundedAmountExpression(pageSql, "p");
        pageSql.Append(" as unfunded, ");
        pageSql.Append(" sum(isnull(p.excess_cash_amount, 0)) as cash_dist, ");
        pageSql.Append(" sum(isnull(p.sales_gain_amount, 0)) as gain_dist, ");
        pageSql.Append(" sum(isnull(p.preferred_return_amount, 0)) as preferred_return, ");
        pageSql.Append(" sum(isnull(p.return_of_capital_amount, 0)) as return_of_capital, ");
        pageSql.Append(" sum(isnull(p.released_capital_amount, 0)) as released, ");
        pageSql.Append(" sum(isnull(p.net_invested_capital_amount, 0)) as net_invested_capital_amount, ");
        pageSql.Append(" sum(isnull(p.preferred_return_amount, 0)) ");
        pageSql.Append(" + sum(isnull(p.sales_gain_amount, 0)) ");
        pageSql.Append(" + sum(isnull(p.excess_cash_amount, 0)) as net_distributed_amount, ");
        PortalPortfolioListSql.AppendReservedAmountExpression(pageSql, "p");
        pageSql.Append(" as reserved_amount ");
        AppendInvestorPortfolioFrom(pageSql, factTable);
        AppendInvestorTransactionWhere(pageSql, view, period, fundKey);
        pageSql.Append(" group by f.fund_key, f.fund_code ");
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorTransactionPageAsync(
            BuildInvestorTransactionCountSql(factTable, view, period, fundKey),
            pageSql,
            investorKey,
            period,
            search,
            fundKey,
            page,
            pageSize,
            static reader => new InvestorFundDistributionsDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                Committed = reader.GetDecimalOrDefault("committed"),
                Unfunded = reader.GetDecimalOrDefault("unfunded"),
                CashDist = reader.GetDecimalOrDefault("cash_dist"),
                GainDist = reader.GetDecimalOrDefault("gain_dist"),
                PreferredReturn = reader.GetDecimalOrDefault("preferred_return"),
                ReturnOfCapital = reader.GetDecimalOrDefault("return_of_capital"),
                Released = reader.GetDecimalOrDefault("released"),
                NetInvestedCapitalAmount = reader.GetDecimalOrDefault("net_invested_capital_amount"),
                NetDistributedAmount = reader.GetDecimalOrDefault("net_distributed_amount"),
                ReservedAmount = reader.GetDecimalOrDefault("reserved_amount")
            });
    }

    public async Task<PagedResult<InvestorFundIrrDto>> GetInvestorIrrAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? sortBy,
        string? sortDir,
        int? fundKey,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseInvestorIrr(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        // IRR columns exist on fact_investor_portfolio_quarterly only.
        // The LTD fact table has no IRR columns, so LTD returns 0 for now.
        var isLtd = view == TimeGranularity.Ltd;
        var factTable = isLtd
            ? WarehouseTables.FactInvestorPortfolioLtd
            : WarehouseTables.FactInvestorPortfolioQuarterly;

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" f.fund_key, ");
        pageSql.Append(" isnull(f.fund_code, '') as fund_code, ");
        pageSql.Append(" max(isnull(f.fund_name, '')) as fund_name, ");
        AppendIrrColumns(pageSql, isLtd);
        AppendInvestorPortfolioFrom(pageSql, factTable);
        AppendInvestorTransactionWhere(pageSql, view, period, fundKey);
        pageSql.Append(" group by f.fund_key, f.fund_code ");
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorTransactionPageAsync(
            BuildInvestorTransactionCountSql(factTable, view, period, fundKey),
            pageSql,
            investorKey,
            period,
            search,
            fundKey,
            page,
            pageSize,
            static reader => new InvestorFundIrrDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                Irr1YearPct = reader.GetNullableDecimal("irr_1_year_pct"),
                Irr3YearPct = reader.GetNullableDecimal("irr_3_year_pct"),
                Irr5YearPct = reader.GetNullableDecimal("irr_5_year_pct"),
                Irr7YearPct = reader.GetNullableDecimal("irr_7_year_pct"),
                Irr10YearPct = reader.GetNullableDecimal("irr_10_year_pct"),
                IrrLtdPct = reader.GetNullableDecimal("irr_ltd_pct")
            });
    }

    private static string PortfolioFactTable(TimeGranularity view) =>
        view == TimeGranularity.Ltd
            ? WarehouseTables.FactInvestorPortfolioLtd
            : WarehouseTables.FactInvestorPortfolioQuarterly;

    private static void AppendInvestorPortfolioFrom(StringBuilder sql, string factTable)
    {
        sql.Append($" from {factTable} p ");
        sql.Append($" inner join {WarehouseTables.DimFund} f on f.fund_key = p.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append($" inner join {WarehouseTables.DimInvestor} i on i.investor_key = p.investor_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
    }

    private static void AppendInvestorTransactionWhere(
        StringBuilder sql,
        TimeGranularity view,
        FundPeriodFilter? period,
        int? fundKey = null)
    {
        sql.Append(" where p.investor_key = @investorKey ");
        AppendPortfolioPeriodFilter(sql, view, period);
        WarehouseSql.AppendFundCodeOrNameSearchFilter(sql, "f");
        AppendInvestorFundKeyFilter(sql, fundKey);
    }

    private static void AppendInvestorFundKeyFilter(StringBuilder sql, int? fundKey, string fundAlias = "f")
    {
        if (fundKey is > 0)
        {
            sql.Append($" and {fundAlias}.fund_key = @fundKey ");
        }
    }

    // LTD fact table has no IRR columns; emit 0 for LTD until those columns are available.
    private static void AppendIrrColumns(StringBuilder sql, bool isLtd)
    {
        if (isLtd)
        {
            sql.Append(" cast(0 as decimal(18,6)) as irr_1_year_pct, ");
            sql.Append(" cast(0 as decimal(18,6)) as irr_3_year_pct, ");
            sql.Append(" cast(0 as decimal(18,6)) as irr_5_year_pct, ");
            sql.Append(" cast(0 as decimal(18,6)) as irr_7_year_pct, ");
            sql.Append(" cast(0 as decimal(18,6)) as irr_10_year_pct, ");
            sql.Append(" cast(0 as decimal(18,6)) as irr_ltd_pct ");
            return;
        }

        sql.Append(" max(p.irr_1_year_pct) as irr_1_year_pct, ");
        sql.Append(" max(p.irr_3_year_pct) as irr_3_year_pct, ");
        sql.Append(" max(p.irr_5_year_pct) as irr_5_year_pct, ");
        sql.Append(" max(p.irr_7_year_pct) as irr_7_year_pct, ");
        sql.Append(" max(p.irr_10_year_pct) as irr_10_year_pct, ");
        sql.Append(" max(p.irr_ltd_pct) as irr_ltd_pct ");
    }

    private static void AppendPortfolioPeriodFilter(StringBuilder sql, TimeGranularity view, FundPeriodFilter? period)
    {
        if (view == TimeGranularity.Quarterly && period?.HasDateKey == true)
        {
            sql.Append(" and p.quarter_year = ( ");
            sql.Append($" select quarter_year from {WarehouseTables.DimDate} where date_key = @dateKey ");
            sql.Append(" ) ");
        }
        else if (view == TimeGranularity.Daily && period?.HasDateKey == true)
        {
            sql.Append(" and p.date_key = @dateKey ");
        }
    }

    private static StringBuilder BuildInvestorTransactionCountSql(
        string factTable,
        TimeGranularity view,
        FundPeriodFilter? period,
        int? fundKey = null)
    {
        var sql = new StringBuilder();
        sql.Append(" select count(*) from ( ");
        sql.Append(" select f.fund_key ");
        AppendInvestorPortfolioFrom(sql, factTable);
        AppendInvestorTransactionWhere(sql, view, period, fundKey);
        sql.Append(" group by f.fund_key, f.fund_code ");
        sql.Append(" ) fund_rows ");
        return sql;
    }

    private async Task<PagedResult<T>> ExecuteInvestorTransactionPageAsync<T>(
        StringBuilder countSql,
        StringBuilder pageSql,
        long investorKey,
        FundPeriodFilter? period,
        string? search,
        int? fundKey,
        int page,
        int pageSize,
        Func<SqlDataReader, T> mapRow)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddInvestorTransactionParameters(countCommand, investorKey, period, searchTerm, fundKey);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddInvestorTransactionParameters(pageCommand, investorKey, period, searchTerm, fundKey);
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<T>();
        await using (var reader = await pageCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                items.Add(mapRow(reader));
            }
        }

        return new PagedResult<T>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private static void AddInvestorTransactionParameters(
        SqlCommand command,
        long investorKey,
        FundPeriodFilter? period,
        string? searchTerm,
        int? fundKey = null)
    {
        command.Parameters.AddWithValue("@investorKey", investorKey);
        command.Parameters.AddWithValue("@dateKey", (object?)period?.DateKey ?? DBNull.Value);
        command.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        command.Parameters.AddWithValue("@fundKey", fundKey is > 0 ? fundKey.Value : DBNull.Value);
    }
}
