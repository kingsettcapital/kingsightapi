namespace kingsightapi.Entities
{
    /// <summary>
    /// Row from wh_gold1.subjective_input.loan_alias_master for API responses.
    /// </summary>
    public sealed class LoanAliasDto
    {
        public long LoanAliasId { get; init; }
        public string LoanAliasName { get; init; } = string.Empty;
        public decimal? SecurityValue { get; init; }
        public int? Units { get; init; }
        public decimal? NetAcres { get; init; }
        public decimal? SquareFeet { get; init; }
        public string CreatedBy { get; init; } = string.Empty;
        public DateTime? CreatedDtm { get; init; }
        public string UpdatedBy { get; init; } = string.Empty;
        public DateTime? UpdatedDtm { get; init; }
    }

    /// <summary>
    /// Body for creating a row — UI sends only loanAliasName (createdBy is ignored; set from JWT).
    /// </summary>
    public sealed class LoanAliasSaveRequest
    {
        public string LoanAliasName { get; init; } = string.Empty;
        public string CreatedBy { get; init; } = string.Empty;
    }

    /// <summary>
    /// Body for updating a row — UI sends only loanAliasName (updatedBy is ignored; set from JWT).
    /// </summary>
    public sealed class LoanAliasUpdateRequest
    {
        public string LoanAliasName { get; init; } = string.Empty;
        public string UpdatedBy { get; init; } = string.Empty;
    }
}
