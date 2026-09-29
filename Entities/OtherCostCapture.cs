namespace kingsightapi.Entities
{
    public sealed class OtherCostCaptureDto
    {
        public long LoanKey { get; init; }
        public string LoanId { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string LoanAliasName { get; init; } = string.Empty;
        public decimal? OutstandingInvoices { get; init; }
        public decimal? EstRealizationCosts { get; init; }
        public decimal? CostToComplete { get; init; }
        public string UserUpdatedBy { get; init; } = string.Empty;
        public DateTime? UserUpdatedDate { get; init; }
    }

    public sealed class OtherCostCaptureUpdateDto
    {
        public long LoanKey { get; init; }
        public string LoanCode { get; init; } = string.Empty;
        public decimal? OutstandingInvoices { get; init; }
        public decimal? EstRealizationCosts { get; init; }
        public decimal? CostToComplete { get; init; }
        public string UserUpdatedBy { get; init; } = string.Empty;
    }

    public sealed class OtherCostCaptureBatchUpdateRequest
    {
        public List<OtherCostCaptureUpdateDto> Loans { get; init; } = [];
    }
}
