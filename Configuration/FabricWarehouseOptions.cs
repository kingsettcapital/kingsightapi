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

    /// <summary>
    /// Subjective input schema database: <c>subjective_input.*</c> master and relationship tables.
    /// </summary>
    public string SubjectiveInputDatabase { get; set; } = "wh_gold1";
}
