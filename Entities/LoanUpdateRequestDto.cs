namespace kingsightapi.Entities
{
    public sealed class LoanUpdateRequestDto
    {
        public long LoanKey { get; init; }
        public string LoanCode { get; init; } = string.Empty;
        public long? LoanAliasKey { get; init; }
        public short? LoanRanking { get; init; }
        public string DummyLoanLink { get; init; } = string.Empty;
        public bool? IsLoanInterestApplicable { get; init; }
        public string LateInterestOffNote { get; init; } = string.Empty;
        /// <summary>
        /// When set, updates <c>shared.dim_loan.funding_status_code</c> (and description when present)
        /// to the matching <c>shared.dim_status</c> row.
        /// </summary>
        public long? FundingStatusKey { get; init; }
        public string UserUpdatedBy { get; init; } = string.Empty;
    }

    public sealed class LoanUpdateBatchRequest
    {
        public List<LoanUpdateRequestDto> Loans { get; init; } = [];

        /// <summary>
        /// Which screen audit columns to stamp: loan_alias | loan_attribute.
        /// When omitted, inferred from whether attribute fields are present.
        /// </summary>
        public string? AuditProfile { get; init; }
    }
}
