namespace kingsightapi.Entities
{
    /// <summary>
    /// Current row from mort.dim_investor for API responses (is_current = 1).
    /// </summary>
    public sealed class InvestorDto
    {
        public long InvestorKey { get; init; }
        public string InvestorCode { get; init; } = string.Empty;
        public string InvestorName { get; init; } = string.Empty;
        public long? InvestorAliasKey { get; init; }
        public string InvestorAliasName { get; init; } = string.Empty;
        public string UserUpdatedBy { get; init; } = string.Empty;
        public DateTime? UserUpdatedDate { get; init; }
    }

    /// <summary>
    /// Body for updating investor_alias_name on a current row in mort.dim_investor.
    /// </summary>
    public sealed class InvestorUpdateRequest
    {
        public int InvestorKey { get; init; }
        public string InvestorAliasName { get; init; } = string.Empty;
        public string UserUpdatedBy { get; init; } = string.Empty;
    }
}
