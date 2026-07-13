namespace kingsightapi.Entities
{
    public sealed class LtvValidationRowDto
    {
        public long LoanKey { get; init; }
        public string? ParentLoanCode { get; init; }
        public string LoanCode { get; init; } = string.Empty;
        public string LoanName { get; init; } = string.Empty;
        public string LoanAliasName { get; init; } = string.Empty;
        public string InvestorAliasName { get; init; } = string.Empty;
        public decimal? SecurityValue { get; init; }
        public decimal? Exposure { get; init; }
        public int? Ranking { get; init; }
        public decimal? Ltv { get; init; }
        public decimal? PriorLtv { get; init; }
        public string? UpdateReason { get; init; }
        public string? UpdateComment { get; init; }
        public string? AiComments { get; init; }
        public decimal? AiConfidenceScore { get; init; }
        public string? QrSlideLink { get; init; }
        public string? UserUpdatedBy { get; init; }
        public DateTime? UserUpdatedDate { get; init; }

        /// <summary>Legacy SPA field — same as <see cref="LoanCode"/>.</summary>
        public string ChildLoanId => LoanCode;

        /// <summary>Legacy SPA field — same as <see cref="LoanName"/>.</summary>
        public string Description => LoanName;

        /// <summary>Legacy SPA field — same as <see cref="ParentLoanCode"/>.</summary>
        public string? ParentLoanId => ParentLoanCode;

        /// <summary>Legacy SPA field — same as <see cref="AiComments"/>.</summary>
        public string? AiCommentary => AiComments;
    }

    public sealed class LtvValidationUpdateItem
    {
        public long LoanKey { get; init; }
        public string? LoanCode { get; init; }
        public decimal? Ltv { get; init; }
        public string? UpdateReason { get; init; }
        public string? UpdateComment { get; init; }
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
