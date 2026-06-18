using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class FundPortalService
{
    public async Task<PagedResult<FundInvestorCapitalActivitiesDto>> GetFundCapitalActivitiesAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseFundCapitalActivities(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var factTable = PortfolioFactTable(view);

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" isnull(cast(i.investor_id as varchar(20)), '') as investor_code, ");
        pageSql.Append(" max(isnull(i.investor_name, '')) as investor_name, ");
        AppendGroupedQuarterYearColumn(pageSql, view, period);
        pageSql.Append(" max(isnull(i.investor_type_name, '')) as type, ");
        pageSql.Append(" sum(isnull(p.capital_called_amount, 0)) as called, ");
        pageSql.Append(" sum(isnull(p.investment_transferred_in_amount, 0)) as transfer_in, ");
        pageSql.Append(" sum(isnull(p.investment_transferred_out_amount, 0)) as transfer_out, ");
        pageSql.Append(" sum(isnull(p.redeemed_amount, 0)) as redemption ");
        AppendFundPortfolioFrom(pageSql, factTable);
        AppendFundTransactionWhere(pageSql, view, period);
        AppendFundTransactionGroupBy(pageSql, view, period);
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundTransactionPageAsync(
            BuildFundTransactionCountSql(factTable, view, period),
            pageSql,
            fundKey,
            period,
            search,
            investorName,
            page,
            pageSize,
            static reader => new FundInvestorCapitalActivitiesDto
            {
                InvestorCode = reader.GetStringOrEmpty("investor_code"),
                InvestorName = reader.GetStringOrEmpty("investor_name"),
                QuarterYear = reader.GetStringOrEmpty("quarter_year"),
                Type = reader.GetStringOrEmpty("type"),
                Called = reader.GetDecimalOrDefault("called"),
                TransferIn = reader.GetDecimalOrDefault("transfer_in"),
                TransferOut = reader.GetDecimalOrDefault("transfer_out"),
                Redemption = reader.GetDecimalOrDefault("redemption")
            });
    }

    public async Task<PagedResult<FundInvestorDistributionsDto>> GetFundDistributionsSummaryAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseFundDistributions(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var factTable = PortfolioFactTable(view);

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" isnull(cast(i.investor_id as varchar(20)), '') as investor_code, ");
        pageSql.Append(" max(isnull(i.investor_name, '')) as investor_name, ");
        AppendGroupedQuarterYearColumn(pageSql, view, period);
        pageSql.Append(" max(isnull(i.investor_type_name, '')) as type, ");
        pageSql.Append(" sum(isnull(p.commitment_amount, 0)) as committed, ");
        pageSql.Append(" sum(isnull(p.commitment_amount, 0)) - sum(isnull(p.capital_called_amount, 0)) as unfunded, ");
        pageSql.Append(" sum(isnull(p.excess_cash_amount, 0)) as cash_dist, ");
        pageSql.Append(" sum(isnull(p.sales_gain_amount, 0)) as gain_dist, ");
        pageSql.Append(" sum(isnull(p.preferred_return_amount, 0)) as preferred_return, ");
        pageSql.Append(" sum(isnull(p.return_of_capital_amount, 0)) as return_of_capital, ");
        pageSql.Append(" sum(isnull(p.released_capital_amount, 0)) as released ");
        AppendFundPortfolioFrom(pageSql, factTable);
        AppendFundTransactionWhere(pageSql, view, period);
        AppendFundTransactionGroupBy(pageSql, view, period);
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundTransactionPageAsync(
            BuildFundTransactionCountSql(factTable, view, period),
            pageSql,
            fundKey,
            period,
            search,
            investorName,
            page,
            pageSize,
            static reader => new FundInvestorDistributionsDto
            {
                InvestorCode = reader.GetStringOrEmpty("investor_code"),
                InvestorName = reader.GetStringOrEmpty("investor_name"),
                QuarterYear = reader.GetStringOrEmpty("quarter_year"),
                Type = reader.GetStringOrEmpty("type"),
                Committed = reader.GetDecimalOrDefault("committed"),
                Unfunded = reader.GetDecimalOrDefault("unfunded"),
                CashDist = reader.GetDecimalOrDefault("cash_dist"),
                GainDist = reader.GetDecimalOrDefault("gain_dist"),
                PreferredReturn = reader.GetDecimalOrDefault("preferred_return"),
                ReturnOfCapital = reader.GetDecimalOrDefault("return_of_capital"),
                Released = reader.GetDecimalOrDefault("released")
            });
    }

    public async Task<PagedResult<FundInvestorIrrDto>> GetFundIrrAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseFundIrr(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var isLtd = view == TimeGranularity.Ltd;
        var factTable = isLtd
            ? WarehouseTables.FactInvestorPortfolioLtd
            : WarehouseTables.FactInvestorPortfolioQuarterly;

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" isnull(cast(i.investor_id as varchar(20)), '') as investor_code, ");
        pageSql.Append(" max(isnull(i.investor_name, '')) as investor_name, ");
        AppendGroupedQuarterYearColumn(pageSql, view, period);
        pageSql.Append(" max(isnull(i.investor_type_name, '')) as type, ");
        AppendIrrColumns(pageSql, isLtd, view, period);
        AppendFundPortfolioFrom(pageSql, factTable);
        AppendFundTransactionWhere(pageSql, view, period);
        AppendFundTransactionGroupBy(pageSql, view, period);
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundTransactionPageAsync(
            BuildFundTransactionCountSql(factTable, view, period),
            pageSql,
            fundKey,
            period,
            search,
            investorName,
            page,
            pageSize,
            static reader => new FundInvestorIrrDto
            {
                InvestorCode = reader.GetStringOrEmpty("investor_code"),
                InvestorName = reader.GetStringOrEmpty("investor_name"),
                QuarterYear = reader.GetStringOrEmpty("quarter_year"),
                Type = reader.GetStringOrEmpty("type"),
                Irr1YearPct = reader.GetNullableDecimal("irr_1_year_pct"),
                Irr3YearPct = reader.GetNullableDecimal("irr_3_year_pct"),
                Irr5YearPct = reader.GetNullableDecimal("irr_5_year_pct"),
                Irr7YearPct = reader.GetNullableDecimal("irr_7_year_pct"),
                Irr10YearPct = reader.GetNullableDecimal("irr_10_year_pct"),
                IrrLtdPct = reader.GetNullableDecimal("irr_ltd_pct")
            });
    }

    public async Task<PagedResult<FundInvestorObligationDto>> GetFundCapitalObligationsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseFundObligations(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var innerSql = new StringBuilder();
        PortalPortfolioTransactionSql.AppendFundObligationsUnion(
            innerSql,
            WarehouseTables.FactInvestorPortfolioQuarterly,
            period);

        return await ExecuteFundUnpivotedPortfolioPageAsync(
            innerSql,
            orderBy,
            fundKey,
            period,
            search,
            investorName,
            page,
            pageSize,
            static reader => new FundInvestorObligationDto
            {
                InvestorCode = reader.GetStringOrEmpty("investor_code"),
                InvestorName = reader.GetStringOrEmpty("investor_name"),
                QuarterYear = reader.GetStringOrEmpty("quarter_year"),
                Type = reader.GetStringOrEmpty("type"),
                Amount = reader.GetDecimalOrDefault("amount")
            });
    }

    public async Task<PagedResult<FundInvestorNetAssetsDto>> GetFundNetAssetsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseFundNetAssets(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var innerSql = new StringBuilder();
        PortalPortfolioTransactionSql.AppendFundNetAssetsUnion(
            innerSql,
            WarehouseTables.FactInvestorPortfolioQuarterly,
            period);

        return await ExecuteFundUnpivotedPortfolioPageAsync(
            innerSql,
            orderBy,
            fundKey,
            period,
            search,
            investorName,
            page,
            pageSize,
            static reader => new FundInvestorNetAssetsDto
            {
                InvestorCode = reader.GetStringOrEmpty("investor_code"),
                InvestorName = reader.GetStringOrEmpty("investor_name"),
                QuarterYear = reader.GetStringOrEmpty("quarter_year"),
                Type = reader.GetStringOrEmpty("type"),
                Ret = reader.GetNullableDecimal("ret")
            },
            valueColumn: "ret");
    }

    private async Task<PagedResult<T>> ExecuteFundUnpivotedPortfolioPageAsync<T>(
        StringBuilder innerSql,
        PortalListOrderBy orderBy,
        int fundKey,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        int page,
        int pageSize,
        Func<SqlDataReader, T> mapRow,
        string valueColumn = "amount")
    {
        var countSql = new StringBuilder();
        countSql.Append(" select count(*) from ( ");
        countSql.Append(innerSql);
        countSql.Append(" ) rows where 1=1 ");
        AppendFundUnpivotedOuterWhere(countSql);

        var pageSql = new StringBuilder();
        pageSql.Append($" select investor_code, investor_name, quarter_year, type, {valueColumn} from ( ");
        pageSql.Append(innerSql);
        pageSql.Append(" ) rows where 1=1 ");
        AppendFundUnpivotedOuterWhere(pageSql);
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundTransactionPageAsync(
            countSql,
            pageSql,
            fundKey,
            period,
            search,
            investorName,
            page,
            pageSize,
            mapRow);
    }

    private static void AppendFundUnpivotedOuterWhere(StringBuilder sql)
    {
        sql.Append(" and (@search is null ");
        sql.Append(" or lower(isnull(rows.investor_code, '')) like '%' + lower(@search) + '%' ");
        sql.Append(" or lower(isnull(rows.investor_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append(" ) ");
        sql.Append(" and (@investorName is null ");
        sql.Append(" or lower(isnull(rows.investor_name, '')) = lower(@investorName) ");
        sql.Append(" ) ");
    }

    private static string PortfolioFactTable(TimeGranularity view) =>
        view == TimeGranularity.Ltd
            ? WarehouseTables.FactInvestorPortfolioLtd
            : WarehouseTables.FactInvestorPortfolioQuarterly;

    private static void AppendFundPortfolioFrom(StringBuilder sql, string factTable)
    {
        sql.Append($" from {factTable} p ");
        sql.Append($" inner join {WarehouseTables.DimFund} f on f.fund_key = p.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append($" inner join {WarehouseTables.DimInvestor} i on i.investor_key = p.investor_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
    }

    private static void AppendGroupedQuarterYearColumn(StringBuilder sql, TimeGranularity view, FundPeriodFilter? period)
    {
        if (PortalPortfolioListSql.GroupsPortfolioByQuarterYear(view, period))
        {
            sql.Append(" isnull(p.quarter_year, '') as quarter_year, ");
            return;
        }

        if (view == TimeGranularity.Ltd)
        {
            sql.Append(" max(isnull(cast(p.date_key as varchar(20)), '')) as quarter_year, ");
            return;
        }

        sql.Append(" max(isnull(p.quarter_year, '')) as quarter_year, ");
    }

    private static void AppendFundTransactionGroupBy(StringBuilder sql, TimeGranularity view, FundPeriodFilter? period)
    {
        sql.Append(" group by i.investor_key, i.investor_id ");
        if (PortalPortfolioListSql.GroupsPortfolioByQuarterYear(view, period))
        {
            sql.Append(", p.quarter_year ");
        }
    }

    private static void AppendFundTransactionWhere(
        StringBuilder sql,
        TimeGranularity view,
        FundPeriodFilter? period,
        bool applyInvestorNameFilter = true)
    {
        sql.Append(" where p.fund_key = @fundKey ");
        AppendPortfolioPeriodFilter(sql, view, period);
        WarehouseSql.AppendInvestorCodeOrNameSearchFilter(sql, "i");
        if (applyInvestorNameFilter)
        {
            WarehouseSql.AppendInvestorNameFilter(sql, "i");
        }
    }

    private static void AppendIrrColumns(StringBuilder sql, bool isLtd, TimeGranularity view, FundPeriodFilter? period)
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

    private static StringBuilder BuildFundTransactionCountSql(string factTable, TimeGranularity view, FundPeriodFilter? period)
    {
        var sql = new StringBuilder();
        sql.Append(" select count(*) from ( ");
        sql.Append(" select i.investor_key ");
        if (PortalPortfolioListSql.GroupsPortfolioByQuarterYear(view, period))
        {
            sql.Append(", p.quarter_year ");
        }

        AppendFundPortfolioFrom(sql, factTable);
        AppendFundTransactionWhere(sql, view, period);
        AppendFundTransactionGroupBy(sql, view, period);
        sql.Append(" ) investor_rows ");
        return sql;
    }

    private async Task<PagedResult<T>> ExecuteFundTransactionPageAsync<T>(
        StringBuilder countSql,
        StringBuilder pageSql,
        int fundKey,
        FundPeriodFilter? period,
        string? search,
        string? investorName,
        int page,
        int pageSize,
        Func<SqlDataReader, T> mapRow)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var investorNameTerm = string.IsNullOrWhiteSpace(investorName) ? null : investorName.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddFundTransactionParameters(countCommand, fundKey, period, searchTerm, investorNameTerm);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddFundTransactionParameters(pageCommand, fundKey, period, searchTerm, investorNameTerm);
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

    private static void AddFundTransactionParameters(
        SqlCommand command,
        int fundKey,
        FundPeriodFilter? period,
        string? searchTerm,
        string? investorNameTerm)
    {
        command.Parameters.AddWithValue("@fundKey", fundKey);
        PortalPortfolioListSql.AddPeriodParameter(command, period);
        command.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        command.Parameters.AddWithValue("@investorName", (object?)investorNameTerm ?? DBNull.Value);
    }
}
