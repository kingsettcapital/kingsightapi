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
        sql.Append($" sum(isnull({factAlias}.reserved_amount, 0)) as reserved_amount, ");
        sql.Append($" sum(isnull({factAlias}.released_capital_amount, 0)) as released_capital_amount ");
    }

    /// <summary>KPI card totals for list page summary (same fact aggregates, summary column names).</summary>
    public static void AppendPortfolioSummaryMetricSums(StringBuilder sql, string factAlias = "a")
    {
        sql.Append($" sum(isnull({factAlias}.commitment_amount, 0)) as total_commitment, ");
        sql.Append($" sum(isnull({factAlias}.net_invested_capital_amount, 0)) as net_invested_capital, ");
        sql.Append($" sum(isnull({factAlias}.preferred_return_amount, 0)) ");
        sql.Append($" + sum(isnull({factAlias}.sales_gain_amount, 0)) ");
        sql.Append($" + sum(isnull({factAlias}.excess_cash_amount, 0)) as net_distributed, ");
        sql.Append($" sum(isnull({factAlias}.reserved_amount, 0)) as reserved_uncalled ");
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
