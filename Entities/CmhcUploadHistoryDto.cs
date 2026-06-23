namespace kingsightapi.Entities
{
    /// <summary>
    /// Row from mort.CMHC_upload_historytbl.
    /// uploadedBy is the stored UNIQUEIDENTIFIER string; uploadedByUserId/uploadedByName are resolved from input.UserMst.
    /// </summary>
    public sealed class CmhcUploadHistoryDto
    {
        public long FileId { get; set; }
        public string Filename { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public int? UploadedByUserId { get; set; }
        public string? UploadedByName { get; set; }
    }
}