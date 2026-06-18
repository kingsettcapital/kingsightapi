using System.Text;
using kingsightapi.Entities;

namespace kingsightapi.Services;

/// <summary>
/// Unpivoted portfolio rows from <c>fact_investor_portfolio_quarterly</c> (obligations and net assets).
/// </summary>
internal static class PortalPortfolioTransactionSql
{
    public static void AppendInvestorObligationsUnion(
        StringBuilder sql,
        string factTable,
        FundPeriodFilter? period)
    {
        AppendBranch(sql, factTable, period, investorScope: true, "Commitment", "p.commitment_amount", "amount", coalesceToZero: true, isFirst: true);
        AppendBranch(sql, factTable, period, investorScope: true, "Unfunded", "p.unfunded_amount", "amount", coalesceToZero: true, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: true, "Reserve", "p.reserved_amount", "amount", coalesceToZero: true, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: true, "Release", "p.released_capital_amount", "amount", coalesceToZero: true, isFirst: false);
    }

    public static void AppendFundObligationsUnion(
        StringBuilder sql,
        string factTable,
        FundPeriodFilter? period)
    {
        AppendBranch(sql, factTable, period, investorScope: false, "Commitment", "p.commitment_amount", "amount", coalesceToZero: true, isFirst: true);
        AppendBranch(sql, factTable, period, investorScope: false, "Unfunded", "p.unfunded_amount", "amount", coalesceToZero: true, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: false, "Reserve", "p.reserved_amount", "amount", coalesceToZero: true, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: false, "Release", "p.released_capital_amount", "amount", coalesceToZero: true, isFirst: false);
    }

    public static void AppendInvestorNetAssetsUnion(
        StringBuilder sql,
        string factTable,
        FundPeriodFilter? period)
    {
        AppendBranch(sql, factTable, period, investorScope: true, "1 Year", "p.irr_1_year_pct", "ret", coalesceToZero: false, isFirst: true);
        AppendBranch(sql, factTable, period, investorScope: true, "3 Year", "p.irr_3_year_pct", "ret", coalesceToZero: false, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: true, "5 Year", "p.irr_5_year_pct", "ret", coalesceToZero: false, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: true, "7 Year", "p.irr_7_year_pct", "ret", coalesceToZero: false, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: true, "10 Year", "p.irr_10_year_pct", "ret", coalesceToZero: false, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: true, "ITD", "p.irr_ltd_pct", "ret", coalesceToZero: false, isFirst: false);
    }

    public static void AppendFundNetAssetsUnion(
        StringBuilder sql,
        string factTable,
        FundPeriodFilter? period)
    {
        AppendBranch(sql, factTable, period, investorScope: false, "1 Year", "p.irr_1_year_pct", "ret", coalesceToZero: false, isFirst: true);
        AppendBranch(sql, factTable, period, investorScope: false, "3 Year", "p.irr_3_year_pct", "ret", coalesceToZero: false, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: false, "5 Year", "p.irr_5_year_pct", "ret", coalesceToZero: false, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: false, "7 Year", "p.irr_7_year_pct", "ret", coalesceToZero: false, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: false, "10 Year", "p.irr_10_year_pct", "ret", coalesceToZero: false, isFirst: false);
        AppendBranch(sql, factTable, period, investorScope: false, "ITD", "p.irr_ltd_pct", "ret", coalesceToZero: false, isFirst: false);
    }

    private static void AppendBranch(
        StringBuilder sql,
        string factTable,
        FundPeriodFilter? period,
        bool investorScope,
        string typeLabel,
        string valueExpression,
        string valueColumn,
        bool coalesceToZero,
        bool isFirst)
    {
        if (!isFirst)
        {
            sql.Append(" union all ");
        }

        sql.Append(" select ");
        if (investorScope)
        {
            sql.Append(" f.fund_key, ");
            sql.Append(" isnull(f.fund_code, '') as fund_code, ");
            sql.Append(" isnull(f.fund_name, '') as fund_name, ");
        }
        else
        {
            sql.Append(" isnull(cast(i.investor_id as varchar(20)), '') as investor_code, ");
            sql.Append(" isnull(i.investor_name, '') as investor_name, ");
        }

        sql.Append(" isnull(p.quarter_year, '') as quarter_year, ");
        sql.Append(" isnull(p.quarter_year, '') as period, ");
        sql.Append($" '{typeLabel}' as type, ");
        if (coalesceToZero)
        {
            sql.Append($" isnull({valueExpression}, 0) as {valueColumn} ");
        }
        else
        {
            sql.Append($" {valueExpression} as {valueColumn} ");
        }

        sql.Append($" from {factTable} p ");
        sql.Append($" inner join {WarehouseTables.DimFund} f on f.fund_key = p.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append($" inner join {WarehouseTables.DimInvestor} i on i.investor_key = p.investor_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
        sql.Append(investorScope ? " where p.investor_key = @investorKey " : " where p.fund_key = @fundKey ");
        PortalPortfolioListSql.AppendPortfolioFactQuarterlyPeriodFilter(sql, period);
    }
}
