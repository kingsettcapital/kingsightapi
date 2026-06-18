namespace kingsightapi.Services;

/// <summary>Fully qualified warehouse table names (dbo schema).</summary>
internal static class WarehouseTables
{
    public const string DimDate = "dbo.dim_date";
    public const string DimFund = "dbo.dim_fund";
    public const string DimInvestor = "dbo.dim_investor";
    public const string DimProperty = "dbo.dim_property";
    public const string DimTransactionType = "dbo.dim_transaction_type";
    public const string FactCommitted = "dbo.fact_commitment";
    public const string FactInvestment = "dbo.fact_investment";
    public const string FactDistribution = "dbo.fact_distribution";
    public const string FactInvestorPortfolioLtd = "dbo.fact_investor_portfolio_ltd";
    public const string FactInvestorPortfolioQuarterly = "dbo.fact_investor_portfolio_quarterly";
    public const string FactFundNav = "dbo.fact_fund_nav";
    public const string FactAssetMetrics = "dbo.fact_asset_metrics";

    // Data Explorer source view (investor + fund + capital data elements).
    public const string ViewInvestorPortfolioLtdSchema = "dbo";
    public const string ViewInvestorPortfolioLtdName = "view_investor_portfolio_ltd";
    public const string ViewInvestorPortfolioLtd = "[dbo].[view_investor_portfolio_ltd]";

    // Data Explorer asset product source view.
    public const string ViewInvestorFundAssetSchema = "dbo";
    public const string ViewInvestorFundAssetName = "view_investor_fund_asset";
    public const string ViewInvestorFundAsset = "[dbo].[view_investor_fund_asset]";

    // Data Explorer saved templates (user metadata).
    public const string DataExplorerTemplate = "dbo.data_explorer_template";
    public const string DataExplorerTemplateColumn = "dbo.data_explorer_template_column";
    public const string DataExplorerTemplateFilter = "dbo.data_explorer_template_filter";
}
