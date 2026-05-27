using System.Text;

namespace kingsightapi.Services;

/// <summary>Shared SQL fragments appended via StringBuilder (dim/fund SCD, fact aggregates).</summary>
internal static class WarehouseSql
{
    public static void AppendCurrentFundFilter(StringBuilder sql, string fundAlias = "f")
    {
        sql.Append(" ( ");
        sql.Append($" isnull({fundAlias}.is_current, 1) = 1 ");
        sql.Append($" or ( ");
        sql.Append($" {fundAlias}.is_current is null ");
        sql.Append($" and getdate() between {fundAlias}.valid_from ");
        sql.Append($" and isnull({fundAlias}.valid_to, cast('9999-12-31' as datetime2)) ");
        sql.Append(" ) ");
        sql.Append(" ) ");
    }

    public static void AppendCurrentInvestorFilter(StringBuilder sql, string investorAlias = "i")
    {
        sql.Append($" isnull({investorAlias}.is_current, 1) = 1 ");
    }

    /// <summary>
    /// Return % when invested_amount_total / invested_amount_fmv_total already exist
    /// (e.g. columns from an outer apply subquery). Do not use in the same SELECT that defines those aliases.
    /// </summary>
    public static void AppendReturnPercentExpression(StringBuilder sql)
    {
        sql.Append(" case ");
        sql.Append(" when abs(isnull(invested_amount_total, 0)) > 0 ");
        sql.Append(" then ((isnull(invested_amount_fmv_total, 0) - isnull(invested_amount_total, 0)) ");
        sql.Append(" / abs(invested_amount_total)) * 100.0 ");
        sql.Append(" else null ");
        sql.Append(" end ");
    }

    /// <summary>Return % for GROUP BY queries — uses sum(invested_amount) inline (same SELECT level).</summary>
    public static void AppendReturnPercentFromFactSums(StringBuilder sql, string factAlias = "f")
    {
        sql.Append(" case ");
        sql.Append($" when abs(isnull(sum(isnull({factAlias}.invested_amount, 0)), 0)) > 0 ");
        sql.Append($" then ((isnull(sum(isnull({factAlias}.invested_amount_fmv, 0)), 0) ");
        sql.Append($" - isnull(sum(isnull({factAlias}.invested_amount, 0)), 0)) ");
        sql.Append($" / abs(sum(isnull({factAlias}.invested_amount, 0)))) * 100.0 ");
        sql.Append(" else null ");
        sql.Append(" end ");
    }

    public static void AppendInvestorSearchFilter(StringBuilder sql, string investorAlias = "i")
    {
        sql.Append(" and (@search is null ");
        sql.Append($" or {investorAlias}.investor_name like '%' + @search + '%' ");
        sql.Append($" or isnull({investorAlias}.investor_short_name, '') like '%' + @search + '%' ");
        sql.Append($" or isnull({investorAlias}.investor_type_name, '') like '%' + @search + '%' ");
        sql.Append($" or isnull({investorAlias}.relationship_name, '') like '%' + @search + '%' ");
        sql.Append(" ) ");
    }

    public static void AppendFundSearchFilter(StringBuilder sql, string fundAlias = "f")
    {
        sql.Append(" and (@search is null ");
        sql.Append($" or {fundAlias}.fund_name like '%' + @search + '%' ");
        sql.Append($" or isnull({fundAlias}.fund_code, '') like '%' + @search + '%' ");
        sql.Append($" or isnull({fundAlias}.fund_strategy_name, '') like '%' + @search + '%' ");
        sql.Append($" or isnull({fundAlias}.fund_type_name, '') like '%' + @search + '%' ");
        sql.Append(" ) ");
    }

    public static void AppendCurrentPropertyFilter(StringBuilder sql, string propertyAlias = "p")
    {
        sql.Append($" isnull({propertyAlias}.is_current, 1) = 1 ");
    }

    public static void AppendPropertySearchFilter(StringBuilder sql, string propertyAlias = "p")
    {
        sql.Append(" and (@search is null ");
        sql.Append($" or {propertyAlias}.property_name like '%' + @search + '%' ");
        sql.Append($" or isnull({propertyAlias}.city, '') like '%' + @search + '%' ");
        sql.Append($" or isnull({propertyAlias}.province, '') like '%' + @search + '%' ");
        sql.Append($" or isnull({propertyAlias}.asset_type, '') like '%' + @search + '%' ");
        sql.Append($" or isnull({propertyAlias}.property_code, '') like '%' + @search + '%' ");
        sql.Append($" or isnull({propertyAlias}.fund, '') like '%' + @search + '%' ");
        sql.Append(" ) ");
    }

    /// <summary>Match dim_property.fund text to dim_fund code or name.</summary>
    public static void AppendPropertyFundJoin(
        StringBuilder sql,
        string propertyAlias = "p",
        string fundAlias = "df")
    {
        sql.Append($" inner join {WarehouseTables.DimFund} {fundAlias} on ");
        sql.Append($" ( isnull({fundAlias}.fund_code, '') = isnull({propertyAlias}.fund, '') ");
        sql.Append($" or isnull({fundAlias}.fund_name, '') = isnull({propertyAlias}.fund, '') ");
        sql.Append($" or isnull({fundAlias}.js_fund_name, '') = isnull({propertyAlias}.fund, '') ) ");
    }

    public static void AppendPropertyBelongsToFundFilter(
        StringBuilder sql,
        string propertyAlias = "p",
        string fundAlias = "f")
    {
        sql.Append($" and isnull({propertyAlias}.fund, '') <> '' ");
        sql.Append($" and ( ");
        sql.Append($" isnull({propertyAlias}.fund, '') = isnull({fundAlias}.fund_code, '') ");
        sql.Append($" or isnull({propertyAlias}.fund, '') = isnull({fundAlias}.fund_name, '') ");
        sql.Append($" or isnull({propertyAlias}.fund, '') = isnull({fundAlias}.js_fund_name, '') ");
        sql.Append(" ) ");
    }
}
