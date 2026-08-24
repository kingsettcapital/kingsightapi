using kingsightapi.Configuration;

namespace kingsightapi.Services;

/// <summary>
/// Enterprise capital / portal table names (<c>dbo.*</c>).
/// Uses two-part names because the Fabric connection context already targets the enterprise warehouse.
/// </summary>
internal static class WarehouseTables
{
    internal static void Configure(FabricWarehouseOptions options)
    {
        // Mortgage <c>Database</c> is used by <see cref="FabricWarehouseTables"/>; portal tables stay dbo.*.
    }

    public static string DimDate => "dbo.dim_date";
    public static string DimFund => "dbo.dim_fund";
    public static string DimInvestor => "dbo.dim_investor";
    public static string DimProperty => "dbo.dim_property";
    /// <summary>Maps leaf <c>property_id</c> to consolidated asset key for Assets list roll-up.</summary>
    public static string StgEntityRelation => "stg.stg_EntityRelation";
    public static string DimTransactionType => "dbo.dim_transaction_type";
    public static string FactCommitted => "dbo.fact_commitment";
    public static string FactInvestment => "dbo.fact_investment";
    public static string FactDistribution => "dbo.fact_distribution";
    public static string FactInvestorPortfolioLtd => "dbo.fact_investor_portfolio_ltd";
    public static string FactInvestorPortfolioQuarterly => "dbo.fact_investor_portfolio_quarterly";
    public static string FactFundNav => "dbo.fact_fund_nav";
    public static string FactAssetMetrics => "dbo.fact_asset_metrics";

    public const string ViewInvestorPortfolioLtdSchema = "dbo";
    public const string ViewInvestorPortfolioLtdName = "view_investor_portfolio_ltd";
    public const string ViewInvestorPortfolioLtd = "[dbo].[view_investor_portfolio_ltd]";

    public const string ViewInvestorFundAssetSchema = "dbo";
    public const string ViewInvestorFundAssetName = "view_investor_fund_asset";
    public const string ViewInvestorFundAsset = "[dbo].[view_investor_fund_asset]";

    public static string DataExplorerTemplate => "dbo.data_explorer_template";
    public static string DataExplorerTemplateColumn => "dbo.data_explorer_template_column";
    public static string DataExplorerTemplateFilter => "dbo.data_explorer_template_filter";
}
