namespace kingsightapi.Entities
{
    public sealed class LtvValidationRowDto
    {
        public long LoanKey { get; init; }
        public string ParentLoanId { get; init; } = string.Empty;
        public string ChildLoanId { get; init; } = string.Empty;

        /// <summary>SPA fallback when childLoanId is empty.</summary>
        public string LoanId => ChildLoanId;

        public string Description { get; init; } = string.Empty;
        public string LoanAliasName { get; init; } = string.Empty;
        public string InvestorAliasName { get; init; } = string.Empty;
        public decimal? SecurityValue { get; init; }
        public decimal? Exposure { get; init; }
        public int? Ranking { get; init; }
        public decimal? Ltv { get; init; }
        public string? AiCommentary { get; init; }
        public string? UserUpdatedBy { get; init; }
        public DateTime? UserUpdatedDate { get; init; }
    }

    public sealed class LtvValidationUpdateItem
    {
        public long LoanKey { get; init; }
        public decimal? Ltv { get; init; }
        public string UserUpdatedBy { get; init; } = "system";
    }

    public sealed class LtvValidationBulkUpdateRequest
    {
        public List<LtvValidationUpdateItem> Loans { get; init; } = [];
    }

    public sealed class LtvValidationConfirmRequest
    {
        public List<long> LoanKeys { get; init; } = [];
        public string UserUpdatedBy { get; init; } = "system";
    }
}
