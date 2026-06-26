namespace kingsightapi.Entities
{
    public sealed class TaxArrearsRowDto
    {
        public long TaxArrearKey { get; init; }
        public long LoanKey { get; init; }
        public string LoanId { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string LoanAliasName { get; init; } = string.Empty;
        public DateTime? TaxMemoDate { get; init; }
        public decimal? TaxArrears { get; init; }
        public string? TaxYear { get; init; }
        public string? Notes { get; init; }
        public string? UserUpdatedBy { get; init; }
        public DateTime? UserUpdatedDate { get; init; }
    }

    public sealed class TaxArrearsLookupsDto
    {
        public IReadOnlyList<string> TaxYears { get; init; } = [];
    }

    public sealed class TaxArrearsCreateRequest
    {
        public long LoanKey { get; init; }
        public string? LoanCode { get; init; }
        public DateTime? TaxMemoDate { get; init; }
        public decimal? TaxArrears { get; init; }
        public string? TaxYear { get; init; }
        public string? Notes { get; init; }
        public string UserUpdatedBy { get; init; } = "system";
    }

    public sealed class TaxArrearsUpdateItem
    {
        public long TaxArrearKey { get; init; }
        public string? LoanCode { get; init; }
        public string? OriginalTaxYear { get; init; }
        public DateTime? TaxMemoDate { get; init; }
        public decimal? TaxArrears { get; init; }
        public string? TaxYear { get; init; }
        public string? Notes { get; init; }
        public string UserUpdatedBy { get; init; } = "system";
    }

    public sealed class TaxArrearsBulkUpdateRequest
    {
        public List<TaxArrearsUpdateItem> TaxArrears { get; init; } = [];
    }
}
