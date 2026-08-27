namespace kingsightapi.Configuration;

/// <summary>
/// Fabric warehouse / lakehouse database names for three-part SQL.
/// Values are environment-specific via <c>appsettings.{Environment}.json</c>
/// (Dev and UAT subjective input: <c>wh_gold</c>).
/// </summary>
public sealed class FabricWarehouseOptions
{
    public const string SectionName = "FabricWarehouse";

    /// <summary>
    /// Primary warehouse: <c>mort.*</c>, <c>input.*</c>, and related Kingsight mortgage tables.
    /// </summary>
    public string Database { get; set; } = "wh_gold";

    /// <summary>
    /// Subjective input schema database: <c>subjective_input.*</c> master and relationship tables.
    /// Dev and UAT = <c>wh_gold</c>.
    /// </summary>
    public string SubjectiveInputDatabase { get; set; } = "wh_gold";

    /// <summary>
    /// Silver lakehouse with Yardi shortcuts (e.g. <c>yardi.collateral</c>, <c>yardi.Collateral_Value</c>).
    /// </summary>
    public string SilverLakehouseDatabase { get; set; } = "shortcut_lh_silver";

    /// <summary>
    /// Bronze lakehouse shortcuts for CMHC watchlist and related external tables.
    /// Development = <c>shortcut_lh_bronze1</c>; UAT = <c>shortcut_lh_bronze</c>.
    /// </summary>
    public string BronzeLakehouseDatabase { get; set; } = "shortcut_lh_bronze1";

    /// <summary>
    /// Schema for bronze CMHC watchlist (<c>cmhc_default</c>).
    /// Development = <c>external_files</c>; UAT = <c>dbo</c>.
    /// </summary>
    public string BronzeExternalFilesSchema { get; set; } = "external_files";
}
