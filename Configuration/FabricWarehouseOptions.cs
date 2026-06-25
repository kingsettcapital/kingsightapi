namespace kingsightapi.Configuration;

/// <summary>
/// Fabric warehouse database name for three-part SQL on mortgage / input schemas.
/// </summary>
public sealed class FabricWarehouseOptions
{
    public const string SectionName = "FabricWarehouse";

    /// <summary>
    /// Primary warehouse: <c>mort.*</c>, <c>input.*</c>, and related Kingsight mortgage tables.
    /// </summary>
    public string Database { get; set; } = "wh_gold";
}
