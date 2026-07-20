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
        string? fundCode,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseInvestorCapitalActivities(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var factTable = PortfolioFactTable(view);

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" f.fund_key, ");
        pageSql.Append(" isnull(f.fund_code, '') as fund_code, ");
        pageSql.Append(" max(isnull(f.fund_name, '')) as fund_name, ");
        AppendGroupedQuarterYearColumn(pageSql, view, period);
        pageSql.Append(" max(isnull(f.fund_type_name, '')) as type, ");
        pageSql.Append(" sum(isnull(p.capital_called_amount, 0)) as called, ");
        pageSql.Append(" sum(isnull(p.investment_transferred_in_amount, 0)) as transfer_in, ");
        pageSql.Append(" sum(isnull(p.investment_transferred_out_amount, 0)) as transfer_out, ");
        pageSql.Append(" sum(isnull(p.redeemed_amount, 0)) as redemption ");
        AppendInvestorPortfolioFrom(pageSql, factTable);
        AppendInvestorTransactionWhere(pageSql, view, period);
        AppendInvestorTransactionGroupBy(pageSql, view, period);
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorTransactionPageAsync(
            BuildInvestorTransactionCountSql(factTable, view, period),
            pageSql,
            investorKey,
            period,
            search,
            fundCode,
            page,
            pageSize,
            static reader => new InvestorFundCapitalActivitiesDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                QuarterYear = reader.GetStringOrEmpty("quarter_year"),
                Period = reader.GetNullableTrimmedString("period"),
                Type = reader.GetStringOrEmpty("type"),
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
        string? fundCode,
        string? sortBy,
        string? sortDir,
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
        AppendGroupedQuarterYearColumn(pageSql, view, period);
        pageSql.Append(" max(isnull(f.fund_type_name, '')) as type, ");
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
        AppendInvestorTransactionWhere(pageSql, view, period);
        AppendInvestorTransactionGroupBy(pageSql, view, period);
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorTransactionPageAsync(
            BuildInvestorTransactionCountSql(factTable, view, period),
            pageSql,
            investorKey,
            period,
            search,
            fundCode,
            page,
            pageSize,
            static reader => new InvestorFundDistributionsDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                QuarterYear = reader.GetStringOrEmpty("quarter_year"),
                Period = reader.GetNullableTrimmedString("period"),
                Type = reader.GetStringOrEmpty("type"),
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
        string? fundCode,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseInvestorIrr(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var isLtd = view == TimeGranularity.Ltd;
        var factTable = isLtd
            ? WarehouseTables.FactInvestorPortfolioLtd
            : WarehouseTables.FactInvestorPortfolioQuarterly;

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" f.fund_key, ");
        pageSql.Append(" isnull(f.fund_code, '') as fund_code, ");
        pageSql.Append(" max(isnull(f.fund_name, '')) as fund_name, ");
        AppendGroupedQuarterYearColumn(pageSql, view, period);
        pageSql.Append(" max(isnull(f.fund_type_name, '')) as type, ");
        AppendIrrColumns(pageSql, isLtd, view, period);
        AppendInvestorPortfolioFrom(pageSql, factTable);
        AppendInvestorTransactionWhere(pageSql, view, period);
        AppendInvestorTransactionGroupBy(pageSql, view, period);
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorTransactionPageAsync(
            BuildInvestorTransactionCountSql(factTable, view, period),
            pageSql,
            investorKey,
            period,
            search,
            fundCode,
            page,
            pageSize,
            static reader => new InvestorFundIrrDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                QuarterYear = reader.GetStringOrEmpty("quarter_year"),
                Period = reader.GetNullableTrimmedString("period"),
                Type = reader.GetStringOrEmpty("type"),
                Irr1YearPct = reader.GetNullableDecimal("irr_1_year_pct"),
                Irr3YearPct = reader.GetNullableDecimal("irr_3_year_pct"),
                Irr5YearPct = reader.GetNullableDecimal("irr_5_year_pct"),
                Irr7YearPct = reader.GetNullableDecimal("irr_7_year_pct"),
                Irr10YearPct = reader.GetNullableDecimal("irr_10_year_pct"),
                IrrLtdPct = reader.GetNullableDecimal("irr_ltd_pct")
            });
    }

    public async Task<PagedResult<InvestorFundObligationDto>> GetInvestorCapitalObligationsAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? fundCode,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseInvestorObligations(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var factTable = view == TimeGranularity.Ltd
            ? WarehouseTables.FactInvestorPortfolioLtd
            : WarehouseTables.FactInvestorPortfolioQuarterly;

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" f.fund_key, ");
        pageSql.Append(" isnull(f.fund_code, '') as fund_code, ");
        pageSql.Append(" max(isnull(f.fund_name, '')) as fund_name, ");
        PortalPortfolioTransactionSql.AppendObligationPeriodColumns(pageSql, view, period);
        PortalPortfolioTransactionSql.AppendObligationMetricAggregates(pageSql);
        AppendInvestorPortfolioFrom(pageSql, factTable);
        AppendInvestorTransactionWhere(pageSql, view, period);
        PortalPortfolioTransactionSql.AppendObligationGroupBy(pageSql, view, period, investorScope: true);
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorTransactionPageAsync(
            BuildInvestorObligationCountSql(factTable, view, period),
            pageSql,
            investorKey,
            period,
            search,
            fundCode,
            page,
            pageSize,
            static reader => new InvestorFundObligationDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                QuarterYear = reader.GetStringOrEmpty("quarter_year"),
                Period = reader.GetStringOrEmpty("period"),
                CommitmentAmount = reader.GetDecimalOrDefault("commitment_amount"),
                UnfundedAmount = reader.GetDecimalOrDefault("unfunded_amount"),
                ReservedAmount = reader.GetDecimalOrDefault("reserved_amount"),
                ReleasedCapitalAmount = reader.GetDecimalOrDefault("released_capital_amount")
            });
    }

    public async Task<PagedResult<InvestorFundNetAssetsDto>> GetInvestorNetAssetsAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? fundCode,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseInvestorNetAssets(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" f.fund_key, ");
        pageSql.Append(" isnull(f.fund_code, '') as fund_code, ");
        pageSql.Append(" max(isnull(f.fund_name, '')) as fund_name, ");
        pageSql.Append(" isnull(d.quarter_year, '') as quarter_year, ");
        pageSql.Append(" isnull(d.quarter_year, '') as period, ");
        pageSql.Append(" sum(isnull(n.nav, 0)) as nav ");
        PortalPortfolioTransactionSql.AppendInvestorNavFrom(pageSql);
        pageSql.Append(" where 1=1 ");
        PortalPortfolioTransactionSql.AppendUnitizedFundFilter(pageSql);
        PortalPortfolioTransactionSql.AppendNavQuarterlyPeriodFilter(pageSql, period);
        WarehouseSql.AppendFundCodeOrNameSearchFilter(pageSql, "f");
        WarehouseSql.AppendFundCodeFilter(pageSql, "f");
        pageSql.Append(" group by f.fund_key, f.fund_code, d.quarter_year ");
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorTransactionPageAsync(
            BuildInvestorNavCountSql(period),
            pageSql,
            investorKey,
            period,
            search,
            fundCode,
            page,
            pageSize,
            static reader => new InvestorFundNetAssetsDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                QuarterYear = reader.GetStringOrEmpty("quarter_year"),
                Period = reader.GetStringOrEmpty("period"),
                Nav = reader.GetDecimalOrDefault("nav")
            });
    }

    private static StringBuilder BuildInvestorObligationCountSql(
        string factTable,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        var sql = new StringBuilder();
        sql.Append(" select count(*) from ( ");
        sql.Append(" select ");
        PortalPortfolioTransactionSql.AppendObligationCountGroupKeys(sql, view, period, investorScope: true);
        AppendInvestorPortfolioFrom(sql, factTable);
        AppendInvestorTransactionWhere(sql, view, period);
        PortalPortfolioTransactionSql.AppendObligationGroupBy(sql, view, period, investorScope: true);
        sql.Append(" ) obligation_rows ");
        return sql;
    }

    private static StringBuilder BuildInvestorNavCountSql(FundPeriodFilter? period)
    {
        var sql = new StringBuilder();
        sql.Append(" select count(*) from ( ");
        sql.Append(" select f.fund_key, d.quarter_year ");
        PortalPortfolioTransactionSql.AppendInvestorNavFrom(sql);
        sql.Append(" where 1=1 ");
        PortalPortfolioTransactionSql.AppendUnitizedFundFilter(sql);
        PortalPortfolioTransactionSql.AppendNavQuarterlyPeriodFilter(sql, period);
        WarehouseSql.AppendFundCodeOrNameSearchFilter(sql, "f");
        WarehouseSql.AppendFundCodeFilter(sql, "f");
        sql.Append(" group by f.fund_key, f.fund_code, d.quarter_year ");
        sql.Append(" ) nav_rows ");
        return sql;
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

    private static void AppendGroupedQuarterYearColumn(StringBuilder sql, TimeGranularity view, FundPeriodFilter? period) =>
        PortalPortfolioListSql.AppendGroupedQuarterYearAndPeriodColumns(sql, view, period);

    private static void AppendInvestorTransactionGroupBy(StringBuilder sql, TimeGranularity view, FundPeriodFilter? period)
    {
        sql.Append(" group by f.fund_key, f.fund_code ");
        if (PortalPortfolioListSql.GroupsPortfolioByQuarterYear(view, period))
        {
            sql.Append(", p.quarter_year ");
        }
    }

    private static void AppendInvestorTransactionWhere(
        StringBuilder sql,
        TimeGranularity view,
        FundPeriodFilter? period,
        bool applyFundCodeFilter = true)
    {
        sql.Append(" where p.investor_key = @investorKey ");
        AppendPortfolioPeriodFilter(sql, view, period);
        WarehouseSql.AppendFundCodeOrNameSearchFilter(sql, "f");
        if (applyFundCodeFilter)
        {
            WarehouseSql.AppendFundCodeFilter(sql, "f");
        }
    }

    // LTD fact table has no IRR columns; emit 0 for LTD until those columns are available.
    private static void AppendIrrColumns(StringBuilder sql, bool isLtd, TimeGranularity view, FundPeriodFilter? period)
    {
        var perQuarter = PortalPortfolioListSql.GroupsPortfolioByQuarterYear(view, period);

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

        if (perQuarter)
        {
            sql.Append(" max(p.irr_1_year_pct) as irr_1_year_pct, ");
            sql.Append(" max(p.irr_3_year_pct) as irr_3_year_pct, ");
            sql.Append(" max(p.irr_5_year_pct) as irr_5_year_pct, ");
            sql.Append(" max(p.irr_7_year_pct) as irr_7_year_pct, ");
            sql.Append(" max(p.irr_10_year_pct) as irr_10_year_pct, ");
            sql.Append(" max(p.irr_ltd_pct) as irr_ltd_pct ");
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
        if (view == TimeGranularity.Quarterly)
        {
            PortalPortfolioListSql.AppendPortfolioFactQuarterlyPeriodFilter(sql, period);
            return;
        }

        if (view == TimeGranularity.Daily && period?.HasDateKey == true)
        {
            sql.Append(" and p.date_key = @dateKey ");
        }
    }

    private static StringBuilder BuildInvestorTransactionCountSql(string factTable, TimeGranularity view, FundPeriodFilter? period)
    {
        var sql = new StringBuilder();
        sql.Append(" select count(*) from ( ");
        sql.Append(" select f.fund_key ");
        if (PortalPortfolioListSql.GroupsPortfolioByQuarterYear(view, period))
        {
            sql.Append(", p.quarter_year ");
        }

        AppendInvestorPortfolioFrom(sql, factTable);
        AppendInvestorTransactionWhere(sql, view, period);
        AppendInvestorTransactionGroupBy(sql, view, period);
        sql.Append(" ) fund_rows ");
        return sql;
    }

    private async Task<PagedResult<T>> ExecuteInvestorTransactionPageAsync<T>(
        StringBuilder countSql,
        StringBuilder pageSql,
        long investorKey,
        FundPeriodFilter? period,
        string? search,
        string? fundCode,
        int page,
        int pageSize,
        Func<SqlDataReader, T> mapRow)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var fundCodeTerm = string.IsNullOrWhiteSpace(fundCode) ? null : fundCode.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddInvestorTransactionParameters(countCommand, investorKey, period, searchTerm, fundCodeTerm);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddInvestorTransactionParameters(pageCommand, investorKey, period, searchTerm, fundCodeTerm);
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
        string? fundCodeTerm)
    {
        command.Parameters.AddWithValue("@investorKey", investorKey);
        PortalPortfolioListSql.AddPeriodParameter(command, period);
        command.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        command.Parameters.AddWithValue("@fundCode", (object?)fundCodeTerm ?? DBNull.Value);
    }
}
