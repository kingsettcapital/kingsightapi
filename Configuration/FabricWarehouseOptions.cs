namespace kingsightapi.Configuration;

/// <summary>
/// Fabric warehouse / lakehouse database names for three-part SQL.
/// Values are environment-specific via <c>appsettings.{Environment}.json</c>
/// (Dev and UAT warehouse: <c>wh_gold</c>).
/// </summary>
public sealed class FabricWarehouseOptions
{
    public const string SectionName = "FabricWarehouse";

    /// <summary>
    /// Gold warehouse for capital (<c>dbo.*</c> / <c>stg.*</c>), mortgage
    /// (<c>mort.*</c>, <c>input.*</c>), and subjective schemas
    /// (<c>subjective_input.*</c>, <c>shared.*</c>, <c>mortgage.*</c>).
    /// Dev and UAT = <c>wh_gold</c>.
    /// </summary>
    public string Database { get; set; } = "wh_gold";

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
