namespace kingsightapi.Entities
{
    public sealed class Loan
    {
        public int LoanKey { get; init; }
        public string LoanCode { get; init; } = string.Empty;
        public string LoanDesc { get; init; } = string.Empty;
        public string LoanAliasName { get; init; } = string.Empty;
        public short LoanRanking { get; init; }
        public short LoanLevel { get; init; }
        public bool IsLeaf { get; init; }
        public string Level1Code { get; init; } = string.Empty;
        public string Level1Desc { get; init; } = string.Empty;
        public string Level2Code { get; init; } = string.Empty;
        public string Level2Desc { get; init; } = string.Empty;
        public string Level3Code { get; init; } = string.Empty;
        public string Level3Desc { get; init; } = string.Empty;
        public string InvestorName { get; init; } = string.Empty;
        public string SponsorName { get; init; } = string.Empty;
        public DateTime? CreatedDate { get; init; }
        public DateTime? LastRefreshedDate { get; init; }
        public DateTime? UserUpdatedDate { get; init; }
        public string UserUpdatedBy { get; init; } = string.Empty;
    }

    public sealed class LoanDto
    {
        public int LoanKey { get; init; }
        public string LoanCode { get; init; } = string.Empty;
        public string InvestorName { get; init; } = string.Empty;
        public string LoanDesc { get; init; } = string.Empty;
        public string LoanAliasName { get; init; } = string.Empty;
        public short LoanRanking { get; init; }
        public DateTime? CreatedDate { get; init; }
        public DateTime? UserUpdatedDate { get; init; }
        public string UserUpdatedBy { get; init; } = string.Empty;

        //public short LoanLevel { get; init; }
        //public bool IsLeaf { get; init; }
        //public string Level1Code { get; init; } = string.Empty;
        //public string Level1Desc { get; init; } = string.Empty;
        //public string Level2Code { get; init; } = string.Empty;
        //public string Level2Desc { get; init; } = string.Empty;
        //public string Level3Code { get; init; } = string.Empty;
        //public string Level3Desc { get; init; } = string.Empty;
        //public string SponsorName { get; init; } = string.Empty;
        //public DateTime? CreatedDate { get; init; }
        //public DateTime? LastRefreshedDate { get; init; }
    }

    public sealed class LoanUpdateRequest
    {
        public string LoanAliasName { get; init; } = string.Empty;
        public short? LoanRanking { get; init; }
        public DateTime? UserUpdatedDate { get; init; }
        public string UserUpdatedBy { get; init; } = string.Empty;
    }

    public sealed class InvestorDto
    {
        public int LoanKey { get; init; }
        public string InvestorName { get; init; } = string.Empty;
        public string InvestorAliasName { get; init; } = string.Empty;
        public DateTime? UserUpdatedDate { get; init; }
        public string UserUpdatedBy { get; init; } = string.Empty;
    }

    public sealed class InvestorAliasUpdateRequest
    {
        public string InvestorAliasName { get; init; } = string.Empty;
        public DateTime? UserUpdatedDate { get; init; }
        public string UserUpdatedBy { get; init; } = string.Empty;
    }

    public sealed class LoanAlias
    {
        public long LoanKey { get; init; }
        public string LoanCode { get; init; } = string.Empty;
        public string LoanAliasName { get; init; } = string.Empty;
        public short? LoanRanking { get; init; }
        public bool? IsLoanInterestApplicable { get; init; }
        public bool? IsDummyLoanIdentifier { get; init; }
        public string LateInterestOffNotes { get; init; } = string.Empty;
        public DateTime? UserUpdatedDate { get; init; }
        public string UserUpdatedBy { get; init; } = string.Empty;
    }

    public sealed class LoanAliasParent
    {
        public List<LoanAlias> LoanAliases { get; init; } = [];
    }

    /// <summary>
    /// Result of a batch loan upsert via PUT /api/loans/loanalias.
    /// </summary>
    public sealed class LoanBatchUpsertResult
    {
        public int UpdatedCount { get; init; }
        public int InsertedCount { get; init; }
        public IReadOnlyList<long> FailedLoanKeys { get; init; } = [];
    }
}