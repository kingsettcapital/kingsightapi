namespace kingsightapi.Configuration
{
    public sealed class CmhcUploadOptions
    {
        public const string SectionName = "CmhcUpload";

        public string LocalStoragePath { get; set; } = @"C:\Kingsight\CmhcUploads";

        public long MaxFileSizeBytes { get; set; } = 52_428_800;

        public string[] AllowedExtensions { get; set; } = [".xlsx", ".xls", ".xlsm"];

        public string TemplateFileName { get; set; } = "CMHC_Upload_Template.xlsx";
    }
}
