namespace kingsightapi.Entities
{
    /// <summary>
    /// Row from wh_gold1.subjective_input.investor_alias_master for API responses.
    /// </summary>
    public sealed class InvestorAliasDto
    {
        public long InvestorAliasId { get; init; }
        public string InvestorAliasName { get; init; } = string.Empty;
        public string CreatedBy { get; init; } = string.Empty;
        public DateTime? CreatedDtm { get; init; }
        public string UpdatedBy { get; init; } = string.Empty;
        public DateTime? UpdatedDtm { get; init; }
    }

    /// <summary>
    /// Body for creating a row in mort.investor_alias_master.
    /// investor_alias_id is assigned server-side as max(investor_alias_id) + 1.
    /// created_by / updated_by are set server-side from the JWT user (client createdBy is ignored).
    /// </summary>
    public sealed class InvestorAliasSaveRequest
    {
        public string InvestorAliasName { get; init; } = string.Empty;
        public string CreatedBy { get; init; } = string.Empty;
    }

    /// <summary>
    /// Body for updating a row in mort.investor_alias_master.
    /// updated_by is set server-side from the JWT user (client updatedBy is ignored).
    /// </summary>
    public sealed class InvestorAliasUpdateRequest
    {
        public string InvestorAliasName { get; init; } = string.Empty;
        public string UpdatedBy { get; init; } = string.Empty;
    }
}
