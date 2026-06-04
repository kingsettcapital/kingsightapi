namespace kingsightapi.Entities
{
    /// <summary>
    /// Row from mort.CMHC_upload_historytbl.
    /// uploadedBy is the UNIQUEIDENTIFIER user id as a string (camelCase JSON for SPA).
    /// </summary>
    public sealed class CmhcUploadHistoryDto
    {
        public long FileId { get; set; }
        public string Filename { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
    }
}