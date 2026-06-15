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
        sql.Append($" or lower(isnull({investorAlias}.investor_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append($" or lower(isnull({investorAlias}.relationship_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append($" or lower(isnull({investorAlias}.contact_first_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append($" or lower(isnull({investorAlias}.contact_last_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append(" ) ");
    }

    public static void AppendInvestorTypeFilter(StringBuilder sql, string investorAlias = "i")
    {
        sql.Append(" and (@investorType is null ");
        sql.Append($" or lower(isnull({investorAlias}.investor_type_name, '')) = lower(@investorType) ");
        sql.Append(" ) ");
    }

    public static void AppendInvestorRelationshipFilter(StringBuilder sql, string investorAlias = "i")
    {
        sql.Append(" and (@relationship is null ");
        sql.Append($" or lower(isnull({investorAlias}.relationship_name, '')) = lower(@relationship) ");
        sql.Append(" ) ");
    }

    public static void AppendFundSearchFilter(StringBuilder sql, string fundAlias = "f")
    {
        sql.Append(" and (@search is null ");
        sql.Append($" or lower(isnull({fundAlias}.fund_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append($" or lower(isnull({fundAlias}.fund_strategy_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append(" ) ");
    }

    public static void AppendFundTypeFilter(StringBuilder sql, string fundAlias = "f")
    {
        sql.Append(" and (@fundType is null ");
        sql.Append($" or lower(isnull({fundAlias}.fund_type_name, '')) = lower(@fundType) ");
        sql.Append(" ) ");
    }

    public static void AppendFundStrategyFilter(StringBuilder sql, string fundAlias = "f")
    {
        sql.Append(" and (@strategy is null ");
        sql.Append($" or lower(isnull({fundAlias}.fund_strategy_name, '')) = lower(@strategy) ");
        sql.Append(" ) ");
    }

    public static void AppendCurrentPropertyFilter(StringBuilder sql, string propertyAlias = "p")
    {
        sql.Append($" isnull({propertyAlias}.is_current, 1) = 1 ");
    }

    public static void AppendPropertySearchFilter(StringBuilder sql, string propertyAlias = "p")
    {
        sql.Append(" and (@search is null ");
        sql.Append($" or lower(isnull({propertyAlias}.property_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append($" or lower(isnull({propertyAlias}.city, '')) like '%' + lower(@search) + '%' ");
        sql.Append($" or lower(isnull({propertyAlias}.geography, '')) like '%' + lower(@search) + '%' ");
        sql.Append($" or lower(isnull({propertyAlias}.asset_type, '')) like '%' + lower(@search) + '%' ");
        sql.Append(" ) ");
    }

    public static void AppendPropertyAssetTypeFilter(StringBuilder sql, string propertyAlias = "p")
    {
        sql.Append(" and (@assetType is null ");
        sql.Append($" or lower(isnull({propertyAlias}.asset_type, '')) = lower(@assetType) ");
        sql.Append(" ) ");
    }

    public static void AppendPropertyInvestmentTypeFilter(StringBuilder sql, string propertyAlias = "p")
    {
        sql.Append(" and (@investmentType is null ");
        sql.Append($" or lower(isnull({propertyAlias}.investment_type, '')) = lower(@investmentType) ");
        sql.Append(" ) ");
    }

    public static void AppendPropertyGeographyFilter(StringBuilder sql, string propertyAlias = "p")
    {
        sql.Append(" and (@geography is null ");
        sql.Append($" or lower(isnull({propertyAlias}.geography, '')) = lower(@geography) ");
        sql.Append(" ) ");
    }

    public static void AppendPropertyStatusFilter(StringBuilder sql, string propertyAlias = "p")
    {
        sql.Append(" and (@status is null ");
        sql.Append($" or lower(isnull({propertyAlias}.property_status, '')) = lower(@status) ");
        sql.Append(" ) ");
    }

    public static void AppendFundCodeSearchFilter(StringBuilder sql, string propertyAlias = "p")
    {
        sql.Append(" and (@fund_code is null ");
        sql.Append($" or lower(isnull({propertyAlias}.fund, '')) like '%' + lower(@fund_code) + '%' ");
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

    /// <summary>Property rows at fund level 000 Property (warehouse may use '000 - Property').</summary>
    public static void AppendPropertyFundLevel000Filter(StringBuilder sql, string propertyAlias = "p")
    {
        sql.Append($" and isnull({propertyAlias}.fund_level, '') in ('000 Property', '000 - Property') ");
    }

    /// <summary>Latest <c>fact_asset_metrics</c> row per property (max <c>date_key</c>).</summary>
    public static void AppendLatestAssetMetricsApply(
        StringBuilder sql,
        string propertyAlias = "p",
        string applyAlias = "metrics")
    {
        sql.Append($" outer apply ( ");
        sql.Append(" select top 1 ");
        sql.Append(" gross_leasable_area_sqft, ");
        sql.Append(" occupied_area_sqft, ");
        sql.Append(" committed_area_sqft, ");
        sql.Append(" vacant_area_sqft, ");
        sql.Append(" total_units, ");
        sql.Append(" occupied_units, ");
        sql.Append(" vacant_units, ");
        sql.Append(" weighted_avg_lease_term_months, ");
        sql.Append(" weighted_avg_lease_term_rent_months ");
        sql.Append($" from {WarehouseTables.FactAssetMetrics} m ");
        sql.Append($" where m.property_key = {propertyAlias}.property_key ");
        sql.Append(" order by m.date_key desc ");
        sql.Append($" ) {applyAlias} ");
    }
}
