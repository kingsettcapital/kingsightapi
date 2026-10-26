namespace kingsightapi.Entities
{
    /// <summary>
    /// Active row from subjective_input.loan_alias_property_image (property photo on the Loan Detail report).
    /// </summary>
    public sealed class PropertyImageDto
    {
        public long PropertyImageId { get; set; }
        public long LoanAliasId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string? UploadedBy { get; set; }
        public DateTime? UploadedDate { get; set; }
    }
}
