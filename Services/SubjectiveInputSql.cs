using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

/// <summary>
/// Fully qualified <c>wh_gold1.subjective_input</c> / <c>wh_gold1.shared</c> table names and join fragments.
/// </summary>
public sealed class SubjectiveInputSql
{
    private static readonly string[] DimLoanCurrentIndicatorCandidates =
    [
        "is_current",
        "scd_cur_ind",
        "current_ind",
        "scd_current_ind"
    ];

    private bool _dimLoanCurrentProbed;

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

    /// <summary>
    /// Current-row indicator on <c>shared.dim_loan</c>, or null when the table has one row per loan
    /// (no SCD current flag).
    /// </summary>
    public string? DimLoanCurrentIndicatorColumn { get; private set; }

    /// <summary>
    /// Probe once for a current-row column. Prefer <c>is_current</c>; fall back to legacy SCD names.
    /// When none exist, joins skip a current filter (warehouse is one row per loan).
    /// </summary>
    public async Task EnsureDimLoanCurrentIndicatorAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (_dimLoanCurrentProbed)
        {
            return;
        }

        DimLoanCurrentIndicatorColumn = await DimLoanColumnProbe.FindFirstAsync(
            connectionString,
            SharedDimLoan,
            DimLoanCurrentIndicatorCandidates,
            cancellationToken);
        _dimLoanCurrentProbed = true;
    }

    /// <summary>Resolve loan_key for API DTOs via <c>shared.dim_loan</c>.</summary>
    public static string LoanKeySelect(string relationshipAlias = "r", string dimLoanAlias = "l") =>
        $"loan_key = isnull({dimLoanAlias}.loan_key, 0)";

    /// <summary>Direct <c>loan_code</c> join — matches subjective-input source SQL.</summary>
    public static string EqualsLoanCode(string leftAlias, string leftColumn, string rightAlias, string rightColumn) =>
        $"{leftAlias}.{leftColumn} = {rightAlias}.{rightColumn}";

    /// <summary>Direct <c>loan_code</c> parameter match.</summary>
    public static string EqualsLoanCodeParam(string tableAlias, string column, string parameterName) =>
        $"{tableAlias}.{column} = {parameterName}";

    /// <summary>
    /// Current-row predicate. When <paramref name="currentIndicatorColumn"/> is null, returns
    /// <c>1 = 1</c> (one row per loan — no SCD filter).
    /// </summary>
    public static string DimLoanIsCurrent(string dimLoanAlias, string? currentIndicatorColumn)
    {
        if (string.IsNullOrWhiteSpace(currentIndicatorColumn))
        {
            return "1 = 1";
        }

        // Compare as varchar so Fabric does not coerce Y/N to int.
        return $"cast({dimLoanAlias}.[{currentIndicatorColumn}] as varchar(10)) in ('1', 'Y', 'y', 'true', 'TRUE')";
    }

    /// <summary>Current-row predicate using the probed column (or no-op when absent).</summary>
    public string DimLoanIsCurrent(string dimLoanAlias) =>
        DimLoanIsCurrent(dimLoanAlias, DimLoanCurrentIndicatorColumn);

    /// <summary>
    /// Prefer current rows when ordering (0 = current).
    /// Uses a CASE expression (not a bare integer) so SQL Server does not treat it as an ORDER BY ordinal.
    /// </summary>
    public string DimLoanCurrentSortRank(string dimLoanAlias) =>
        string.IsNullOrWhiteSpace(DimLoanCurrentIndicatorColumn)
            ? "case when 1 = 1 then 0 else 1 end"
            : $"case when {DimLoanIsCurrent(dimLoanAlias)} then 0 else 1 end";

    /// <summary>
    /// Best <c>shared.dim_loan</c> row per relationship <c>loan_code</c> using direct equality.
    /// Current row first (when an indicator exists), then highest <c>loan_key</c>.
    /// </summary>
    public string SharedDimLoanOuterApplyOnLoanCode(
        string relationshipAlias = "r",
        string dimLoanAlias = "l",
        IReadOnlyList<string>? extraColumns = null)
    {
        var extraSelect = extraColumns is { Count: > 0 }
            ? ", " + string.Join(", ", extraColumns.Select(column => $"ck.[{column}]"))
            : string.Empty;

        // Bare integer ORDER BY n is a select-list ordinal in SQL Server; always use an expression.
        var orderBy = string.IsNullOrWhiteSpace(DimLoanCurrentIndicatorColumn)
            ? "ck.loan_key desc"
            : $"{DimLoanCurrentSortRank("ck")}, ck.loan_key desc";

        return $"""
        outer apply (
            select top (1)
                   ck.loan_key,
                   ck.parent_loan_code,
                   ck.investor_code{extraSelect}
            from {SharedDimLoan} ck
            where {EqualsVarchar(relationshipAlias, "loan_code", "ck", "loan_code")}
            order by {orderBy}
        ) {dimLoanAlias}
        """;
    }

    public string SharedDimLoanJoinOnLoanCode(string relationshipAlias = "r", string dimLoanAlias = "l") =>
        $"left join {SharedDimLoan} {dimLoanAlias} on {EqualsVarchar(relationshipAlias, "loan_code", dimLoanAlias, "loan_code")} and {DimLoanIsCurrent(dimLoanAlias)}";

    /// <summary>Unfiltered <c>shared.dim_loan</c> join on <c>loan_code</c>.</summary>
    public string SharedDimLoanJoinOnLoanCodeUnfiltered(string relationshipAlias = "r", string dimLoanAlias = "l") =>
        $"left join {SharedDimLoan} {dimLoanAlias} on {EqualsVarchar(relationshipAlias, "loan_code", dimLoanAlias, "loan_code")}";

    public string MortgageDimInvestorJoinOnInvestorCode(string dimLoanAlias = "l", string investorAlias = "i") =>
        $"left join {MortgageDimInvestor} {investorAlias} on {dimLoanAlias}.investor_code = {investorAlias}.investor_code";

    public string LegacyDimInvestorJoinOnInvestorCode(string dimLoanAlias = "l", string investorAlias = "i2") =>
        $"left join {LegacyDimInvestor} {investorAlias} on {dimLoanAlias}.investor_code = {investorAlias}.investor_code";

    public string LoanAliasMasterJoinOnName(string relationshipAlias = "r", string masterAlias = "m") =>
        $"left join {LoanAliasMaster} {masterAlias} on {relationshipAlias}.loan_alias_name = {masterAlias}.loan_alias_name";

    public string InvestorAliasMasterJoinOnName(string relationshipAlias = "r", string masterAlias = "m") =>
        $"left join {InvestorAliasMaster} {masterAlias} on {relationshipAlias}.investor_alias_name = {masterAlias}.investor_alias_name";

    public string InvestorAliasRelationshipJoinOnInvestorCode(string sharedDimLoanAlias = "c", string investorRelAlias = "d") =>
        $"left join {InvestorAliasRelationship} {investorRelAlias} on {EqualsLoanCode(sharedDimLoanAlias, "investor_code", investorRelAlias, "investor_code")}";

    /// <summary>Collation-safe varchar compare for cross-table code joins.</summary>
    public static string EqualsVarchar(string leftAlias, string leftColumn, string rightAlias, string rightColumn) =>
        $"cast({leftAlias}.{leftColumn} as varchar(100)) collate database_default = cast({rightAlias}.{rightColumn} as varchar(100)) collate database_default";

    /// <summary>Collation-safe compare between a table column and a SQL parameter (e.g. <c>@loan_code</c>).</summary>
    public static string EqualsVarcharParam(string tableAlias, string column, string parameterName) =>
        $"cast({tableAlias}.{column} as varchar(100)) collate database_default = cast({parameterName} as varchar(100)) collate database_default";
}
