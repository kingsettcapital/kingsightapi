namespace kingsightapi.Entities
{
    /// <summary>
    /// Loan attribute assignment row from <c>wh_gold1.subjective_input.loan_alias_relationship</c>
    /// with <c>loan_key</c> / investor resolved via <c>wh_gold1.shared.dim_loan</c>.
    /// </summary>
    public sealed class LoanDto
    {
        public long LoanKey { get; init; }
        public string LoanCode { get; init; } = string.Empty;
        public string LoanDesc { get; init; } = string.Empty;
        public long? LoanAliasKey { get; init; }
        public string LoanAliasName { get; init; } = string.Empty;
        public string InvestorName { get; init; } = string.Empty;
        public string InvestorAliasName { get; init; } = string.Empty;
        public short? LoanRanking { get; init; }
        public string DummyLoanLink { get; init; } = string.Empty;
        public bool? IsLoanInterestApplicable { get; init; }
        public string LateInterestOffNote { get; init; } = string.Empty;
        public string UserUpdatedBy { get; init; } = string.Empty;
        public DateTime? UserUpdatedDate { get; init; }
        /// <summary>True when the row comes from <c>external_serviced_loan</c> (Non-KS).</summary>
        public bool IsNonKs { get; init; }
    }

    /// <summary>Dropdown options for Loan Attribute Assignment (from <c>loan_alias_master</c>).</summary>
    public sealed class LoanAliasOptionDto
    {
        public long LoanAliasId { get; init; }
        public string LoanAliasName { get; init; } = string.Empty;
    }

    public sealed class LoanLookupsDto
    {
        public IReadOnlyList<LoanAliasOptionDto> LoanAliases { get; init; } = [];
    }
}
