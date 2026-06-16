using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

/// <summary>Shared SQL for investor/fund module list pages (fact_investor_portfolio_ltd | quarterly).</summary>
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
        AppendReservedAmountAggregate(sql, factAlias);
        sql.Append(", ");
        AppendUnfundedAmountAggregate(sql, factAlias);
        sql.Append(", ");
        sql.Append($" sum(isnull({factAlias}.released_capital_amount, 0)) as released_capital_amount ");
    }

    /// <summary>
    /// Reserved uncalled: warehouse <c>reserved_amount</c> when populated (may be negative);
    /// otherwise commitment minus capital called (legacy SPA derivation).
    /// </summary>
    public static void AppendReservedAmountAggregate(StringBuilder sql, string factAlias = "a")
    {
        AppendReservedAmountExpression(sql, factAlias);
        sql.Append(" as reserved_amount ");
    }

    public static void AppendReservedAmountExpression(StringBuilder sql, string factAlias = "a")
    {
        sql.Append(" case ");
        sql.Append($" when abs(case ");
        sql.Append($" when sum(case when {factAlias}.reserved_amount is not null then 1 else 0 end) > 0 ");
        sql.Append($" then sum(isnull({factAlias}.reserved_amount, 0)) ");
        sql.Append($" else 0 end) > 0 ");
        sql.Append($" then case ");
        sql.Append($" when sum(case when {factAlias}.reserved_amount is not null then 1 else 0 end) > 0 ");
        sql.Append($" then sum(isnull({factAlias}.reserved_amount, 0)) ");
        sql.Append($" else 0 end ");
        sql.Append($" else sum(isnull({factAlias}.commitment_amount, 0)) ");
        sql.Append($" - sum(isnull({factAlias}.capital_called_amount, 0)) ");
        sql.Append(" end ");
    }

    /// <summary>
    /// Unfunded commitment: warehouse <c>unfunded_amount</c> when populated;
    /// otherwise commitment minus capital called (matches unfunded-commitments when column is empty).
    /// </summary>
    public static void AppendUnfundedAmountAggregate(StringBuilder sql, string factAlias = "a")
    {
        AppendUnfundedAmountExpression(sql, factAlias);
        sql.Append(" as unfunded_amount ");
    }

    public static void AppendUnfundedAmountExpression(StringBuilder sql, string factAlias = "a")
    {
        sql.Append(" case ");
        sql.Append($" when abs(case ");
        sql.Append($" when sum(case when {factAlias}.unfunded_amount is not null then 1 else 0 end) > 0 ");
        sql.Append($" then sum(isnull({factAlias}.unfunded_amount, 0)) ");
        sql.Append($" else 0 end) > 0 ");
        sql.Append($" then case ");
        sql.Append($" when sum(case when {factAlias}.unfunded_amount is not null then 1 else 0 end) > 0 ");
        sql.Append($" then sum(isnull({factAlias}.unfunded_amount, 0)) ");
        sql.Append($" else 0 end ");
        sql.Append($" else sum(isnull({factAlias}.commitment_amount, 0)) ");
        sql.Append($" - sum(isnull({factAlias}.capital_called_amount, 0)) ");
        sql.Append(" end ");
    }

    /// <summary>KPI card totals for list page summary (same fact aggregates, summary column names).</summary>
    public static void AppendPortfolioSummaryMetricSums(StringBuilder sql, string factAlias = "a")
    {
        sql.Append($" sum(isnull({factAlias}.commitment_amount, 0)) as total_commitment, ");
        sql.Append($" sum(isnull({factAlias}.net_invested_capital_amount, 0)) as net_invested_capital, ");
        sql.Append($" sum(isnull({factAlias}.preferred_return_amount, 0)) ");
        sql.Append($" + sum(isnull({factAlias}.sales_gain_amount, 0)) ");
        sql.Append($" + sum(isnull({factAlias}.excess_cash_amount, 0)) as net_distributed, ");
        AppendReservedAmountExpression(sql, factAlias);
        sql.Append(" as reserved_uncalled, ");
        AppendUnfundedAmountExpression(sql, factAlias);
        sql.Append(" as unfunded, ");
        sql.Append($" sum(isnull({factAlias}.released_capital_amount, 0)) as released_capital ");
    }

    public static void AppendQuarterlyPeriodFilter(StringBuilder sql, TimeGranularity view, FundPeriodFilter? period)
    {
        if (view != TimeGranularity.Quarterly || period?.HasDateKey != true)
        {
            return;
        }

        sql.Append(" and quarter_year = ( ");
        sql.Append($" select quarter_year from {WarehouseTables.DimDate} where date_key = @dateKey ");
        sql.Append(" ) ");
    }

    public static void AddPeriodParameter(SqlCommand command, FundPeriodFilter? period) =>
        command.Parameters.AddWithValue("@dateKey", period?.HasDateKey == true ? period.DateKey!.Value : DBNull.Value);
}
