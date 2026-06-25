namespace kingsightapi.Entities
{
    public sealed class LoanUpdateRequestDto
    {
        public long LoanKey { get; init; }
        public string LoanCode { get; init; } = string.Empty;
        public int? LoanAliasKey { get; init; }
        public short? LoanRanking { get; init; }
        public string DummyLoanLink { get; init; } = string.Empty;
        public bool? IsLoanInterestApplicable { get; init; }
        public string LateInterestOffNote { get; init; } = string.Empty;
        public string UserUpdatedBy { get; init; } = string.Empty;
    }

    public sealed class LoanUpdateBatchRequest
    {
        public List<LoanUpdateRequestDto> Loans { get; init; } = [];
    }
}
