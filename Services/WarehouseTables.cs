using kingsightapi.Configuration;

namespace kingsightapi.Services;

/// <summary>
/// Capital portal / Data Explorer table names as three-part
/// <c>{FabricWarehouse:Database}.schema.table</c> (default <c>wh_gold</c>).
/// Dimensions live in <c>shared</c> / <c>investor_servicing</c>; facts in <c>investor_servicing</c>.
/// </summary>
internal static class WarehouseTables
{
    private static string _database = "wh_gold";

    internal static void Configure(FabricWarehouseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _database = string.IsNullOrWhiteSpace(options.Database)
            ? "wh_gold"
            : options.Database.Trim();
    }

    /// <summary>Warehouse catalog used for capital portal SQL (<c>FabricWarehouse:Database</c>).</summary>
    public static string Database => _database;

    private static string Shared(string table) => $"{_database}.shared.{table}";

    private static string InvestorServicing(string table) => $"{_database}.investor_servicing.{table}";

    private static string Dbo(string table) => $"{_database}.dbo.{table}";

    public static string DimDate => Shared("dim_date");
    public static string DimFund => Shared("dim_fund");
    public static string DimInvestor => InvestorServicing("dim_investor");
    public static string DimProperty => Shared("dim_property");
    /// <summary>Maps leaf <c>property_key</c> to consolidated asset for Assets list roll-up.</summary>
    public static string DimOwnershipHierarchy => Shared("dim_ownership_hierarchy");
    public static string DimTransactionType => InvestorServicing("dim_transaction_type");
    public static string FactCommitted => InvestorServicing("fact_commitment");
    public static string FactInvestment => InvestorServicing("fact_investment");
    public static string FactDistribution => InvestorServicing("fact_distribution");
    /// <summary>ITD portfolio fact (warehouse name <c>fact_investor_portfolio_itd</c>).</summary>
    public static string FactInvestorPortfolioLtd => InvestorServicing("fact_investor_portfolio_itd");
    public static string FactInvestorPortfolioQuarterly => InvestorServicing("fact_investor_portfolio_quarterly");
    public static string FactFundNav => InvestorServicing("fact_fund_nav");
    public static string FactFundFinancialItd => InvestorServicing("fact_fund_financial_itd");
    public static string FactFundFinancialQuarterly => InvestorServicing("fact_fund_financial_quarterly");
    public static string FactAssetMetrics => InvestorServicing("fact_asset_metrics");
    public static string FactAssetFinancialKsItd => InvestorServicing("fact_asset_financial_ks_itd");
    public static string FactAssetAcquisition => InvestorServicing("fact_asset_acquisition");
    public static string FactAssetSale => InvestorServicing("fact_asset_sale");

    /// <summary>Fund financial metrics (<c>fn_fund_financial</c>) — ITD or quarter_year label.</summary>
    public static string FnFundFinancial => InvestorServicing("fn_fund_financial");

    /// <summary>Asset financial metrics at KS ownership share (<c>fn_asset_financial_ks</c>).</summary>
    public static string FnAssetFinancialKs => InvestorServicing("fn_asset_financial_ks");

    /// <summary>Asset financial metrics grossed up to 100% (<c>fn_asset_financial_100pct</c>).</summary>
    public static string FnAssetFinancial100Pct => InvestorServicing("fn_asset_financial_100pct");

    /// <summary>Asset acquisition event snapshot (<c>fn_asset_acquisition</c>).</summary>
    public static string FnAssetAcquisition => InvestorServicing("fn_asset_acquisition");

    /// <summary>Asset sale event snapshot (<c>fn_asset_sale</c>).</summary>
    public static string FnAssetSale => InvestorServicing("fn_asset_sale");

    /// <summary>Per-fund SharePoint library URL for Interim/Annual Reports.</summary>
    public static string FundSharePointLibrary => InvestorServicing("fund_sharepoint_library");

    /// <summary>Cached SharePoint document rows for fund drill-down Documents.</summary>
    public static string FundDocument => InvestorServicing("fund_document");

    public const string ViewInvestorPortfolioLtdSchema = "investor_servicing";
    public const string ViewInvestorPortfolioLtdName = "vw_investor_portfolio_itd";
    public static string ViewInvestorPortfolioLtd =>
        $"[{_database}].[{ViewInvestorPortfolioLtdSchema}].[{ViewInvestorPortfolioLtdName}]";

    public const string ViewInvestorFundAssetSchema = "investor_servicing";
    public const string ViewInvestorFundAssetName = "vw_investor_fund_asset";
    public static string ViewInvestorFundAsset =>
        $"[{_database}].[{ViewInvestorFundAssetSchema}].[{ViewInvestorFundAssetName}]";

    // Templates still under dbo when present; keep qualified for Initial Catalog safety.
    public static string DataExplorerTemplate => Dbo("data_explorer_template");
    public static string DataExplorerTemplateColumn => Dbo("data_explorer_template_column");
    public static string DataExplorerTemplateFilter => Dbo("data_explorer_template_filter");
}
