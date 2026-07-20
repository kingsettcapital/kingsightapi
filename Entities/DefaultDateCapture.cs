namespace kingsightapi.Entities
{
    public sealed class DefaultDateCaptureRowDto
    {
        public long LoanKey { get; init; }
        public string LoanId { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string LoanAliasName { get; init; } = string.Empty;
        public DateTime? LoanTermDefaultDate { get; init; }
        public DateTime? DefaultDate { get; init; }
        public string? UserUpdatedBy { get; init; }
        public DateTime? UserUpdatedDate { get; init; }
    }

    public sealed class DefaultDateCaptureUpdateItem
    {
        public long LoanKey { get; init; }
        public string LoanCode { get; init; } = string.Empty;
        public DateTime? DefaultDate { get; init; }
        public string UserUpdatedBy { get; init; } = "system";
    }

    public sealed class DefaultDateCaptureBulkUpdateRequest
    {
        public List<DefaultDateCaptureUpdateItem> Loans { get; init; } = [];
    }
}
