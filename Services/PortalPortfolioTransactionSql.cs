using System.Text;
using kingsightapi.Entities;

namespace kingsightapi.Services;

/// <summary>Capital obligations (wide portfolio metrics) and unitized fund NAV for net-assets tables.</summary>
internal static class PortalPortfolioTransactionSql
{
    public static void AppendObligationMetricAggregates(StringBuilder sql, string factAlias = "p")
    {
        sql.Append($" sum(isnull({factAlias}.commitment_amount, 0)) as commitment_amount, ");
        sql.Append($" sum(isnull({factAlias}.unfunded_amount, 0)) as unfunded_amount, ");
        sql.Append($" sum(isnull({factAlias}.reserved_amount, 0)) as reserved_amount, ");
        sql.Append($" sum(isnull({factAlias}.released_capital_amount, 0)) as released_capital_amount ");
    }

    /// <summary>LTD uses literal <c>LTD</c>; quarterly uses <c>quarter_year</c> from portfolio facts.</summary>
    public static void AppendObligationPeriodColumns(
        StringBuilder sql,
        TimeGranularity view,
        FundPeriodFilter? period,
        string factAlias = "p")
    {
        if (view == TimeGranularity.Ltd)
        {
            sql.Append(" 'ITD' as period, ");
            sql.Append(" cast('' as varchar(50)) as quarter_year, ");
            return;
        }

        PortalPortfolioListSql.AppendGroupedQuarterYearAndPeriodColumns(sql, view, period, factAlias);
    }

    public static void AppendObligationGroupBy(
        StringBuilder sql,
        TimeGranularity view,
        FundPeriodFilter? period,
        bool investorScope)
    {
        if (investorScope)
        {
            sql.Append(" group by f.fund_key, f.fund_code ");
        }
        else
        {
            sql.Append(" group by i.investor_key, i.investor_id ");
        }

        if (view == TimeGranularity.Quarterly
            && PortalPortfolioListSql.GroupsPortfolioByQuarterYear(view, period))
        {
            sql.Append(", p.quarter_year ");
        }
    }

    public static void AppendObligationCountGroupKeys(
        StringBuilder sql,
        TimeGranularity view,
        FundPeriodFilter? period,
        bool investorScope)
    {
        if (investorScope)
        {
            sql.Append(" f.fund_key ");
        }
        else
        {
            sql.Append(" i.investor_key ");
        }

        if (view == TimeGranularity.Quarterly
            && PortalPortfolioListSql.GroupsPortfolioByQuarterYear(view, period))
        {
            sql.Append(", p.quarter_year ");
        }
    }

    public static void AppendNavQuarterlyPeriodFilter(
        StringBuilder sql,
        FundPeriodFilter? period,
        string dateAlias = "d")
    {
        if (period?.HasDateKey == true)
        {
            sql.Append($" and {dateAlias}.date_key = @dateKey ");
            return;
        }

        if (period?.HasCalendarYear == true)
        {
            sql.Append($" and {dateAlias}.calendar_year = @calendarYear ");
        }
    }

    public static void AppendUnitizedFundFilter(StringBuilder sql, string fundAlias = "f")
    {
        sql.Append($" and lower(isnull({fundAlias}.fund_type_name, '')) = 'unitized' ");
    }

    public static void AppendInvestorNavFrom(StringBuilder sql)
    {
        sql.Append($" from {WarehouseTables.FactFundNav} n ");
        sql.Append($" inner join {WarehouseTables.DimFund} f on f.fund_key = n.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append($" inner join {WarehouseTables.FactInvestorPortfolioLtd} p on p.fund_key = n.fund_key ");
        sql.Append(" and p.investor_key = @investorKey ");
        sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = n.date_key ");
    }

    public static void AppendFundNavFrom(StringBuilder sql)
    {
        sql.Append($" from {WarehouseTables.FactFundNav} n ");
        sql.Append($" inner join {WarehouseTables.DimFund} f on f.fund_key = n.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" and f.fund_key = @fundKey ");
        sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = n.date_key ");
    }
}
