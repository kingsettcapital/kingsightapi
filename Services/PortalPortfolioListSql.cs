using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

/// <summary>Shared SQL for investor/fund module list pages (fact_investor_portfolio_itd | quarterly).</summary>
internal static class PortalPortfolioListSql
{
    public static string PortfolioTable(TimeGranularity view) =>
        view == TimeGranularity.Quarterly
            ? WarehouseTables.FactInvestorPortfolioQuarterly
            : WarehouseTables.FactInvestorPortfolioLtd;

    public static void AppendPortfolioMetricAggregates(StringBuilder sql, string factAlias = "a")
    {
        sql.Append($" sum(isnull({factAlias}.commitment_amount, 0)) as commitment_amount, ");
        sql.Append($" sum(isnull({factAlias}.net_invested_capital_amount, 0)) as net_invested_capital_amount, ");
        sql.Append($" sum(isnull({factAlias}.preferred_return_amount, 0)) ");
        sql.Append($" + sum(isnull({factAlias}.sales_gain_amount, 0)) ");
        sql.Append($" + sum(isnull({factAlias}.excess_cash_amount, 0)) as net_distributed_amount, ");
        sql.Append($" sum(isnull({factAlias}.reserved_amount, 0)) as reserved_amount, ");
        sql.Append($" sum(isnull({factAlias}.unfunded_amount, 0)) as unfunded_amount, ");
        sql.Append($" sum(isnull({factAlias}.released_capital_amount, 0)) as released_capital_amount ");

        //AppendReservedAmountAggregate(sql, factAlias);
        //sql.Append(", ");
        //AppendUnfundedAmountAggregate(sql, factAlias);
        //sql.Append(", ");
    }

    /// <summary>
    /// Reserved: warehouse <c>reserved_amount</c> from portfolio facts (may be negative).
    /// </summary>
    //public static void AppendReservedAmountAggregate(StringBuilder sql, string factAlias = "a")
    //{
    //    AppendReservedAmountExpression(sql, factAlias);
    //    sql.Append(" as reserved_amount ");
    //}

    //public static void AppendReservedAmountExpression(StringBuilder sql, string factAlias = "a")
    //{
    //    sql.Append($" sum(isnull({factAlias}.reserved_amount, 0)) ");
    //}

    /// <summary>
    /// Unfunded commitment: warehouse <c>unfunded_amount</c> when populated;
    /// otherwise commitment minus capital called (matches unfunded-commitments when column is empty).
    /// </summary>
    //public static void AppendUnfundedAmountAggregate(StringBuilder sql, string factAlias = "a")
    //{
    //    AppendUnfundedAmountExpression(sql, factAlias);
    //    sql.Append(" as unfunded_amount ");
    //}

    //public static void AppendUnfundedAmountExpression(StringBuilder sql, string factAlias = "a")
    //{
    //    sql.Append(" case ");
    //    sql.Append($" when abs(case ");
    //    sql.Append($" when sum(case when {factAlias}.unfunded_amount is not null then 1 else 0 end) > 0 ");
    //    sql.Append($" then sum(isnull({factAlias}.unfunded_amount, 0)) ");
    //    sql.Append($" else 0 end) > 0 ");
    //    sql.Append($" then case ");
    //    sql.Append($" when sum(case when {factAlias}.unfunded_amount is not null then 1 else 0 end) > 0 ");
    //    sql.Append($" then sum(isnull({factAlias}.unfunded_amount, 0)) ");
    //    sql.Append($" else 0 end ");
    //    sql.Append($" else sum(isnull({factAlias}.commitment_amount, 0)) ");
    //    sql.Append($" - sum(isnull({factAlias}.capital_called_amount, 0)) ");
    //    sql.Append(" end ");
    //}

    /// <summary>KPI card totals for list page summary (same fact aggregates, summary column names).</summary>
    public static void AppendPortfolioSummaryMetricSums(StringBuilder sql, string factAlias = "a")
    {
        sql.Append($" sum(isnull({factAlias}.commitment_amount, 0)) as total_commitment, ");
        sql.Append($" sum(isnull({factAlias}.net_invested_capital_amount, 0)) as net_invested_capital, ");
        sql.Append($" sum(isnull({factAlias}.preferred_return_amount, 0)) ");
        sql.Append($" + sum(isnull({factAlias}.sales_gain_amount, 0)) ");
        sql.Append($" + sum(isnull({factAlias}.excess_cash_amount, 0)) as net_distributed, ");
        sql.Append($" sum(isnull({factAlias}.reserved_amount, 0)) as reserved, ");
        sql.Append($" sum(isnull({factAlias}.unfunded_amount, 0)) as unfunded, ");
        //AppendReservedAmountExpression(sql, factAlias);
        //sql.Append(" as reserved, ");
        //AppendUnfundedAmountExpression(sql, factAlias);
        //sql.Append(" as unfunded, ");
        sql.Append($" sum(isnull({factAlias}.released_capital_amount, 0)) as released_capital ");
    }

    public static void AppendQuarterlyPeriodFilter(
        StringBuilder sql,
        TimeGranularity view,
        FundPeriodFilter? period,
        string factAlias = "a")
    {
        if (view != TimeGranularity.Quarterly)
        {
            return;
        }

        AppendPortfolioFactQuarterlyPeriodFilter(sql, period, factAlias);
    }

    /// <summary>Filters portfolio fact rows to one quarter (<c>dateKey</c>) or all quarters in a year (<c>calendarYear</c>).</summary>
    public static void AppendPortfolioFactQuarterlyPeriodFilter(
        StringBuilder sql,
        FundPeriodFilter? period,
        string factAlias = "p")
    {
        if (period?.HasDateKey == true)
        {
            sql.Append($" and {WarehouseSql.QuarterYearEquals($"{factAlias}.quarter_year", $"(select quarter_year from {WarehouseTables.DimDate} where date_key = @dateKey)")} ");
            return;
        }

        if (period?.HasCalendarYear == true)
        {
            sql.Append($" and {factAlias}.quarter_year collate {WarehouseSql.QuarterYearCollation} in ( ");
            sql.Append($" select distinct quarter_year collate {WarehouseSql.QuarterYearCollation} from {WarehouseTables.DimDate} where calendar_year = @calendarYear ");
            sql.Append(" ) ");
        }
    }

    /// <summary>Quarterly view without a specific <c>dateKey</c> returns one row per quarter (not aggregated across the year).</summary>
    public static bool GroupsPortfolioByQuarterYear(TimeGranularity view, FundPeriodFilter? period) =>
        view == TimeGranularity.Quarterly && period?.HasDateKey != true;

    public static void AppendGroupedQuarterYearAndPeriodColumns(
        StringBuilder sql,
        TimeGranularity view,
        FundPeriodFilter? period,
        string factAlias = "p")
    {
        if (GroupsPortfolioByQuarterYear(view, period))
        {
            sql.Append($" isnull({factAlias}.quarter_year, '') as quarter_year, ");
            sql.Append($" isnull({factAlias}.quarter_year, '') as period, ");
            return;
        }

        if (view == TimeGranularity.Ltd)
        {
            sql.Append($" max(isnull(cast({factAlias}.date_key as varchar(20)), '')) as quarter_year, ");
            sql.Append(" 'ITD' as period, ");
            return;
        }

        if (view == TimeGranularity.Quarterly)
        {
            sql.Append($" max(isnull({factAlias}.quarter_year, '')) as quarter_year, ");
            sql.Append($" max(isnull({factAlias}.quarter_year, '')) as period, ");
            return;
        }

        sql.Append($" max(isnull({factAlias}.quarter_year, '')) as quarter_year, ");
        sql.Append(" cast(null as varchar(100)) as period, ");
    }

    public static void AddPeriodParameter(SqlCommand command, FundPeriodFilter? period)
    {
        command.Parameters.AddWithValue("@dateKey", period?.HasDateKey == true ? period.DateKey!.Value : DBNull.Value);
        command.Parameters.AddWithValue(
            "@calendarYear",
            period?.HasCalendarYear == true ? period.CalendarYear!.Value : DBNull.Value);
    }
}
