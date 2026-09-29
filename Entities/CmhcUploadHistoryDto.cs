namespace kingsightapi.Entities
{
    /// <summary>
    /// Row from subjective_input.file_upload_history (or legacy mort.CMHC_upload_historytbl).
    /// uploadedBy is the stored UNIQUEIDENTIFIER string; uploadedByUserId/uploadedByName are resolved from subjective_input.user_master.
    /// </summary>
    public sealed class CmhcUploadHistoryDto
    {
        public long FileId { get; set; }
        public string Filename { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public int? UploadedByUserId { get; set; }
        public string? UploadedByName { get; set; }

        /// <summary>As-of period for the upload (date only, yyyy-MM-dd).</summary>
        public string? AsOfDate { get; set; }
    }
}