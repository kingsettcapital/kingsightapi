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
        ExternalServicedLoan = tables.SubjectiveInput("external_serviced_loan");
        SharedDimLoan = tables.Shared("dim_loan");
        MortgageDimInvestor = tables.Mortgage("dim_investor");
        LegacyDimInvestor = tables.Mort("dim_investor");
        DimStatus = tables.Shared("dim_status");
    }

    public string LoanAliasMaster { get; }
    public string InvestorAliasMaster { get; }
    public string LoanAliasRelationship { get; }
    public string InvestorAliasRelationship { get; }
    public string LoanTaxDetails { get; }
    public string ExternalServicedLoan { get; }
    public string SharedDimLoan { get; }
    public string MortgageDimInvestor { get; }
    public string LegacyDimInvestor { get; }
    public string DimStatus { get; }

    /// <summary>Resolve loan_key for API DTOs via <c>wh_gold1.shared.dim_loan</c>.</summary>
    public static string LoanKeySelect(string relationshipAlias = "r", string dimLoanAlias = "l") =>
        $"loan_key = isnull({dimLoanAlias}.loan_key, 0)";

    /// <summary>Current SCD row — works when <c>scd_cur_ind</c> is bit (1) or varchar ('Y').</summary>
    public static string DimLoanIsCurrent(string dimLoanAlias) =>
        $"cast({dimLoanAlias}.scd_cur_ind as varchar(10)) in ('1', 'Y')";

    public string SharedDimLoanJoinOnLoanCode(string relationshipAlias = "r", string dimLoanAlias = "l") =>
        $"left join {SharedDimLoan} {dimLoanAlias} on {EqualsVarchar(relationshipAlias, "loan_code", dimLoanAlias, "loan_code")} and {DimLoanIsCurrent(dimLoanAlias)}";

    /// <summary>Unfiltered <c>wh_gold1.shared.dim_loan</c> join on <c>loan_code</c> (subjective-input source SQL).</summary>
    public string SharedDimLoanJoinOnLoanCodeUnfiltered(string relationshipAlias = "r", string dimLoanAlias = "l") =>
        $"left join {SharedDimLoan} {dimLoanAlias} on {relationshipAlias}.loan_code = {dimLoanAlias}.loan_code";

    public string MortgageDimInvestorJoinOnInvestorCode(string dimLoanAlias = "l", string investorAlias = "i") =>
        $"left join {MortgageDimInvestor} {investorAlias} on {dimLoanAlias}.investor_code = {investorAlias}.investor_code";

    public string LegacyDimInvestorJoinOnInvestorCode(string dimLoanAlias = "l", string investorAlias = "i2") =>
        $"left join {LegacyDimInvestor} {investorAlias} on {dimLoanAlias}.investor_code = {investorAlias}.investor_code";

    public string LoanAliasMasterJoinOnName(string relationshipAlias = "r", string masterAlias = "m") =>
        $"left join {LoanAliasMaster} {masterAlias} on {relationshipAlias}.loan_alias_name = {masterAlias}.loan_alias_name";

    public string InvestorAliasMasterJoinOnName(string relationshipAlias = "r", string masterAlias = "m") =>
        $"left join {InvestorAliasMaster} {masterAlias} on {relationshipAlias}.investor_alias_name = {masterAlias}.investor_alias_name";

    public string InvestorAliasRelationshipJoinOnInvestorCode(string sharedDimLoanAlias = "c", string investorRelAlias = "d") =>
        $"left join {InvestorAliasRelationship} {investorRelAlias} on {EqualsVarchar(sharedDimLoanAlias, "investor_code", investorRelAlias, "investor_code")}";

    /// <summary>Collation-safe varchar compare for cross-table code joins.</summary>
    public static string EqualsVarchar(string leftAlias, string leftColumn, string rightAlias, string rightColumn) =>
        $"cast({leftAlias}.{leftColumn} as varchar(100)) collate database_default = cast({rightAlias}.{rightColumn} as varchar(100)) collate database_default";

    /// <summary>Collation-safe compare between a table column and a SQL parameter (e.g. <c>@loan_code</c>).</summary>
    public static string EqualsVarcharParam(string tableAlias, string column, string parameterName) =>
        $"cast({tableAlias}.{column} as varchar(100)) collate database_default = cast({parameterName} as varchar(100)) collate database_default";
}
