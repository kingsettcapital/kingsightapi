namespace kingsightapi.Services;

/// <summary>
/// Fully qualified <c>wh_gold1.subjective_input</c> / <c>wh_gold1.shared</c> table names and join fragments.
/// </summary>
public sealed class SubjectiveInputSql
{
    public SubjectiveInputSql(FabricWarehouseTables tables)
    {
        LoanAliasMaster = tables.SubjectiveInput("loan_alias_master");
        InvestorAliasMaster = tables.SubjectiveInput("investor_alias_master");
        LoanAliasRelationship = tables.SubjectiveInput("loan_alias_relationship");
        InvestorAliasRelationship = tables.SubjectiveInput("investor_alias_relationship");
        LoanTaxDetails = tables.SubjectiveInput("loan_tax_details");
        SharedDimLoan = tables.Shared("dim_loan");
        DimStatus = tables.Mort("dim_status");
    }

    public string LoanAliasMaster { get; }
    public string InvestorAliasMaster { get; }
    public string LoanAliasRelationship { get; }
    public string InvestorAliasRelationship { get; }
    public string LoanTaxDetails { get; }
    public string SharedDimLoan { get; }
    public string DimStatus { get; }

    /// <summary>Resolve loan_key for API DTOs via <c>wh_gold1.shared.dim_loan</c>.</summary>
    public static string LoanKeySelect(string relationshipAlias = "r", string dimLoanAlias = "l") =>
        $"loan_key = isnull({dimLoanAlias}.loan_key, 0)";

    /// <summary>Current SCD row — works when <c>scd_cur_ind</c> is bit (1) or varchar ('Y').</summary>
    public static string DimLoanIsCurrent(string dimLoanAlias) =>
        $"cast({dimLoanAlias}.scd_cur_ind as varchar(10)) in ('1', 'Y')";

    public string SharedDimLoanJoinOnLoanCode(string relationshipAlias = "r", string dimLoanAlias = "l") =>
        $"left join {SharedDimLoan} {dimLoanAlias} on {EqualsVarchar(relationshipAlias, "loan_code", dimLoanAlias, "loan_code")} and {DimLoanIsCurrent(dimLoanAlias)}";

    public string LoanAliasMasterJoinOnName(string relationshipAlias = "r", string masterAlias = "m") =>
        $"left join {LoanAliasMaster} {masterAlias} on {relationshipAlias}.loan_alias_name = {masterAlias}.loan_alias_name";

    public string InvestorAliasMasterJoinOnName(string relationshipAlias = "r", string masterAlias = "m") =>
        $"left join {InvestorAliasMaster} {masterAlias} on {relationshipAlias}.investor_alias_name = {masterAlias}.investor_alias_name";

    public string InvestorAliasRelationshipJoinOnInvestorCode(string sharedDimLoanAlias = "c", string investorRelAlias = "d") =>
        $"left join {InvestorAliasRelationship} {investorRelAlias} on {EqualsVarchar(sharedDimLoanAlias, "investor_code", investorRelAlias, "investor_code")}";

    /// <summary>Collation-safe varchar compare for cross-table code joins.</summary>
    public static string EqualsVarchar(string leftAlias, string leftColumn, string rightAlias, string rightColumn) =>
        $"cast({leftAlias}.{leftColumn} as varchar(100)) collate database_default = cast({rightAlias}.{rightColumn} as varchar(100)) collate database_default";
}
