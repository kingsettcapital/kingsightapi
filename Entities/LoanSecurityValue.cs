namespace kingsightapi.Entities
{
    /// <summary>
    /// Loan alias security value row for the Security Value screen.
    /// Collateral Per Yardi is the latest Real Estate collateral_amount sum from Yardi;
    /// security_value / units / acres / SF are stored on loan_alias_master
    /// (security_value falls back to collateral when null or 0).
    /// </summary>
    public sealed class LoanSecurityValueDto
    {
        public long LoanAliasId { get; init; }
        public string LoanAliasName { get; init; } = string.Empty;
        public decimal CollateralPerYardi { get; init; }
        public decimal? SecurityValue { get; init; }
        public int? Units { get; init; }
        public decimal? SquareFeet { get; init; }
        public decimal? Acres { get; init; }
        public string UpdatedBy { get; init; } = string.Empty;
        public DateTime? UpdatedDtm { get; init; }
    }

    public sealed class LoanSecurityValueUpdateDto
    {
        public long LoanAliasId { get; init; }
        public decimal? SecurityValue { get; init; }
        public int? Units { get; init; }
        public decimal? SquareFeet { get; init; }
        public decimal? Acres { get; init; }
        public string UpdatedBy { get; init; } = string.Empty;
    }

    public sealed class LoanSecurityValueBatchUpdateRequest
    {
        public List<LoanSecurityValueUpdateDto> LoanSecurityValues { get; init; } = [];
    }

    /// <summary>
    /// Filter option from wh_gold1.shared.dim_status (value = status_key; label = status_name).
    /// Loan rows filter via shared.dim_loan.funding_status_code = dim_status.status_key.
    /// Use <see cref="LoanSecurityValueStatusTokens.NullValue"/> to filter loans with no status on dim_loan.
    /// </summary>
    public sealed class LoanSecurityValueStatusOptionDto
    {
        public string Value { get; init; } = string.Empty;
        public string DisplayLabel { get; init; } = string.Empty;
    }

    public static class LoanSecurityValueStatusTokens
    {
        /// <summary>Query token for loans where the dim_loan status FK column IS NULL.</summary>
        public const string NullValue = "(null)";
    }
}
