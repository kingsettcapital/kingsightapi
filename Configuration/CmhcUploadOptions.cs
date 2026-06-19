namespace kingsightapi.Configuration;

public sealed class CmhcUploadOptions
{
    public const string SectionName = "CmhcUpload";

    /// <summary><c>Fabric</c> (OneLake) or <c>Local</c> (dev fallback).</summary>
    public string StorageProvider { get; set; } = "Fabric";

    /// <summary>Local disk path when <see cref="StorageProvider"/> is <c>Local</c>.</summary>
    public string LocalStoragePath { get; set; } = @"C:\Kingsight\CmhcUploads";

    /// <summary>OneLake DFS endpoint.</summary>
    public string FabricServiceUri { get; set; } = "https://onelake.dfs.fabric.microsoft.com";

    /// <summary>Fabric workspace (group) id.</summary>
    public string FabricWorkspaceId { get; set; } = "e9c14968-68a1-48d8-8bc8-b81663f54ce3";

    /// <summary>Fabric lakehouse item id.</summary>
    public string FabricLakehouseId { get; set; } = "cc29573e-7312-4261-8c99-212d2c3a0e17";

    /// <summary>Path under lakehouse <c>Files/</c> (no leading slash), e.g. <c>excel_files/cmhc</c>.</summary>
    public string FabricFilesPath { get; set; } = "excel_files/cmhc";

    public long MaxFileSizeBytes { get; set; } = 52_428_800;

    public string[] AllowedExtensions { get; set; } = [".xlsx", ".xls", ".xlsm"];

    public string TemplateFileName { get; set; } = "CMHC_Upload_Template.xlsx";

    public bool UsesFabricStorage =>
        StorageProvider.Equals("Fabric", StringComparison.OrdinalIgnoreCase);

    /// <summary>OneLake filesystem name — the Fabric workspace id (GUID).</summary>
    public string FabricFileSystemName => FabricWorkspaceId.Trim();

    /// <summary>
    /// OneLake directory path under the workspace filesystem.
    /// With GUIDs, item type suffix is omitted: <c>{lakehouseId}/Files/{path}</c>.
    /// </summary>
    public string FabricUploadDirectoryPath
    {
        get
        {
            var lakehouseId = FabricLakehouseId.Trim();
            if (lakehouseId.EndsWith(".Lakehouse", StringComparison.OrdinalIgnoreCase))
            {
                lakehouseId = lakehouseId[..^".Lakehouse".Length];
            }

            var filesPath = FabricFilesPath.Trim().Trim('/');
            return $"{lakehouseId}/Files/{filesPath}";
        }
    }
}
