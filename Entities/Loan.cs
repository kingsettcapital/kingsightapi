namespace kingsightapi.Entities
{
    /// <summary>
    /// Current loan row from mort.dim_loan for alias assignment (is_current = 1).
    /// </summary>
    public sealed class LoanDto
    {
        public long LoanKey { get; init; }
        public string LoanCode { get; init; } = string.Empty;
        public string LoanDesc { get; init; } = string.Empty;
        public int? LoanAliasKey { get; init; }
        public string LoanAliasName { get; init; } = string.Empty;
        public string InvestorName { get; init; } = string.Empty;
        public short? LoanRanking { get; init; }
        public string DummyLoanLink { get; init; } = string.Empty;
        public bool? IsLoanInterestApplicable { get; init; }
        public string LateInterestOffNote { get; init; } = string.Empty;
        public string UserUpdatedBy { get; init; } = string.Empty;
        public DateTime? UserUpdatedDate { get; init; }
    }
}
