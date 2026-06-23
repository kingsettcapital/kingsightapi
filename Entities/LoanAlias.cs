namespace kingsightapi.Entities
{
    /// <summary>
    /// Row from mort.loan_alias_master for API responses.
    /// </summary>
    public sealed class LoanAliasDto
    {
        public long LoanAliasId { get; init; }
        public string LoanAliasName { get; init; } = string.Empty;
        public string CreatedBy { get; init; } = string.Empty;
        public DateTime? CreatedDtm { get; init; }
        public string UpdatedBy { get; init; } = string.Empty;
        public DateTime? UpdatedDtm { get; init; }
    }

    /// <summary>
    /// Body for creating a row in mort.loan_alias_master.
    /// loan_alias_id is assigned server-side as max(loan_alias_id) + 1.
    /// created_by / updated_by are set server-side from the JWT user (client createdBy is ignored).
    /// </summary>
    public sealed class LoanAliasSaveRequest
    {
        public string LoanAliasName { get; init; } = string.Empty;
        public string CreatedBy { get; init; } = string.Empty;
    }

    /// <summary>
    /// Body for updating a row in mort.loan_alias_master.
    /// updated_by is set server-side from the JWT user (client updatedBy is ignored).
    /// </summary>
    public sealed class LoanAliasUpdateRequest
    {
        public string LoanAliasName { get; init; } = string.Empty;
        public string UpdatedBy { get; init; } = string.Empty;
    }
}
