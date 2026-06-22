namespace kingsightapi.Configuration;

public sealed class CmhcUploadOptions
{
    public const string SectionName = "CmhcUpload";

    /// <summary><c>Fabric</c> (OneLake) or <c>Local</c> (dev fallback).</summary>
    public string StorageProvider { get; set; } = "Fabric";

    /// <summary>Local disk root when <see cref="StorageProvider"/> is <c>Local</c>.</summary>
    public string LocalStoragePath { get; set; } = @"C:\Kingsight\CmhcUploads";

    /// <summary>OneLake DFS endpoint.</summary>
    public string FabricServiceUri { get; set; } = "https://onelake.dfs.fabric.microsoft.com";

    /// <summary>Fabric workspace (group) id.</summary>
    public string FabricWorkspaceId { get; set; } = "e9c14968-68a1-48d8-8bc8-b81663f54ce3";

    /// <summary>Fabric lakehouse item id.</summary>
    public string FabricLakehouseId { get; set; } = "cc29573e-7312-4261-8c99-212d2c3a0e17";

    /// <summary>Parent folder under lakehouse <c>Files/</c> and local storage root.</summary>
    public string UploadParentDirectory { get; set; } = "Uploaded files";

    /// <summary>Child folder for CMHC Excel uploads.</summary>
    public string ExcelFilesDirectory { get; set; } = "excel files";

    /// <summary>Child folder for QR slide deck uploads.</summary>
    public string QrSlidesFilesDirectory { get; set; } = "QR Slides Files";

    public long MaxFileSizeBytes { get; set; } = 62_914_560;

    public string[] AllowedExtensions { get; set; } = [".xlsx", ".xls", ".xlsm"];

    public string[] QrSlidesAllowedExtensions { get; set; } = [".pdf"];

    public string TemplateFileName { get; set; } = "CMHC_Upload_Template.xlsx";

    public bool UsesFabricStorage =>
        StorageProvider.Equals("Fabric", StringComparison.OrdinalIgnoreCase);

    /// <summary>OneLake filesystem name — the Fabric workspace id (GUID).</summary>
    public string FabricFileSystemName => FabricWorkspaceId.Trim();

    /// <summary>OneLake path for CMHC Excel uploads under <c>Files/</c>.</summary>
    public string FabricUploadDirectoryPath =>
        BuildFabricUploadDirectoryPath(GetUploadRelativePath(CmhcUploadCategory.CmhcExcel));

    public string GetFabricUploadDirectoryPath(string fileType) =>
        BuildFabricUploadDirectoryPath(GetUploadRelativePath(fileType));

    /// <summary>Relative path under storage root: <c>Uploaded files/excel files</c> or QR folder.</summary>
    public string GetUploadRelativePath(string? fileType)
    {
        var child = IsQrSlidesUpload(fileType)
            ? QrSlidesFilesDirectory
            : ExcelFilesDirectory;

        return CombinePathSegments(UploadParentDirectory, child);
    }

    public IReadOnlyList<string> GetAllowedExtensions(string fileType) =>
        IsQrSlidesUpload(fileType) ? QrSlidesAllowedExtensions : AllowedExtensions;

    public static bool IsQrSlidesUpload(string? fileType) =>
        string.Equals(fileType?.Trim(), CmhcUploadCategory.QrSlides, StringComparison.OrdinalIgnoreCase);

    private static string CombinePathSegments(string parent, string child) =>
        $"{parent.Trim().Trim('/')}/{child.Trim().Trim('/')}";

    private string BuildFabricUploadDirectoryPath(string filesPath)
    {
        var lakehouseId = FabricLakehouseId.Trim();
        if (lakehouseId.EndsWith(".Lakehouse", StringComparison.OrdinalIgnoreCase))
        {
            lakehouseId = lakehouseId[..^".Lakehouse".Length];
        }

        var normalizedPath = filesPath.Trim().Trim('/');
        return $"{lakehouseId}/Files/{normalizedPath}";
    }
}

/// <summary>Upload category tokens passed to storage services (not folder names).</summary>
public static class CmhcUploadCategory
{
    public const string CmhcExcel = "cmhc";
    public const string QrSlides = "qr-slides";
}
