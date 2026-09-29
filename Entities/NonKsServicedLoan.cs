using System.Text.Json.Serialization;
using kingsightapi.Configuration;

namespace kingsightapi.Entities
{
    public sealed class NonKsServicedLoanLookupsDto
    {
        public string NextExtLoanCode { get; init; } = "NKSLn-1";
        /// <summary>Unique sponsors from Yardi view + existing Non-KS rows.</summary>
        public IReadOnlyList<string> Sponsors { get; init; } = [];
    }

    public sealed class NonKsServicedLoanRowDto
    {
        [JsonConverter(typeof(LongAsStringJsonConverter))]
        public long NonKsServicedLoanKey { get; init; }
        /// <summary>Loan alias dropdown value (maps to loan_alias_name).</summary>
        public string? LoanAliasName { get; init; }
        public string? LoanName { get; init; }
        public DateTime? AsAtDate { get; init; }
        public string? LoanId { get; init; }
        public string? LoanCode { get; init; }
        public string? ExtLoanCode { get; init; }
        public string? ServicerId { get; init; }
        public string? Description { get; init; }
        /// <summary>Investor name selected on Non-KS entry (maps to investor / investor_alias_name).</summary>
        public string? InvestorAliasName { get; init; }
        public string? Investor { get; init; }
        public string? InvestorCode { get; init; }
        public string? Sponsor { get; init; }
        public DateTime? DateOfDefault { get; init; }
        public DateTime? MaturityDate { get; init; }
        public DateTime? InterestOffDate { get; init; }
        public DateTime? TaxMemoDate { get; init; }
        public decimal? SecurityValue { get; init; }
        public int? Units { get; init; }
        public decimal? NetAcres { get; init; }
        public decimal? SquareFeet { get; init; }
        public decimal? InterestRate { get; init; }
        public decimal? CurrentLtv { get; init; }
        public decimal? PrincipalBalance { get; init; }
        public decimal? OutstandingInterest { get; init; }
        public decimal? AccruedInterest { get; init; }
        public decimal? LateInterest { get; init; }
        public decimal? OutstandingInvoices { get; init; }
        public decimal? EstRealizationCosts { get; init; }
        public decimal? CostToComplete { get; init; }
        public decimal? TaxArrears { get; init; }
        public decimal? InterestAsOfTaxMemo { get; init; }
        public decimal? InterestAdjustment { get; init; }
        /// <summary><c>subjective_input.external_serviced_loan.funding_status</c>.</summary>
        public string? FundingStatus { get; init; }
        public string? UserUpdatedBy { get; init; }
        public DateTime? UserUpdatedDate { get; init; }
        public string? CreatedBy { get; init; }
        public DateTime? CreatedDate { get; init; }
    }

    public class NonKsServicedLoanCreateItem
    {
        /// <summary>Loan alias (SPA sends as loanName).</summary>
        public string? LoanAliasName { get; init; }
        public string? LoanName { get; init; }
        public DateTime? AsAtDate { get; init; }
        public string? LoanId { get; init; }
        public string? LoanCode { get; init; }
        public string? ExtLoanCode { get; init; }
        public string? ServicerId { get; init; }
        public string? Description { get; init; }
        /// <summary>Investor name selected on Non-KS entry (maps to investor / investor_alias_name).</summary>
        public string? InvestorAliasName { get; init; }
        public string? Investor { get; init; }
        public string? InvestorCode { get; init; }
        public string? Sponsor { get; init; }
        public DateTime? DateOfDefault { get; init; }
        public DateTime? MaturityDate { get; init; }
        public DateTime? InterestOffDate { get; init; }
        public DateTime? TaxMemoDate { get; init; }
        public decimal? SecurityValue { get; init; }
        public int? Units { get; init; }
        public decimal? NetAcres { get; init; }
        public decimal? SquareFeet { get; init; }
        public decimal? InterestRate { get; init; }
        public decimal? CurrentLtv { get; init; }
        public decimal? PrincipalBalance { get; init; }
        public decimal? OutstandingInterest { get; init; }
        public decimal? AccruedInterest { get; init; }
        public decimal? LateInterest { get; init; }
        public decimal? OutstandingInvoices { get; init; }
        public decimal? EstRealizationCosts { get; init; }
        public decimal? CostToComplete { get; init; }
        public decimal? TaxArrears { get; init; }
        public decimal? InterestAsOfTaxMemo { get; init; }
        public decimal? InterestAdjustment { get; init; }
        /// <summary>Saved to <c>external_serviced_loan.funding_status</c> (dim_status status_name).</summary>
        public string? FundingStatus { get; init; }
        public string UserUpdatedBy { get; init; } = "system";
    }

    public sealed class NonKsServicedLoanUpdateItem : NonKsServicedLoanCreateItem
    {
        [JsonConverter(typeof(LongAsStringJsonConverter))]
        public long? NonKsServicedLoanKey { get; init; }

        /// <summary>Original as-at date used to locate the row when the date is edited.</summary>
        public DateTime? OriginalAsAtDate { get; init; }
    }

    public sealed class NonKsServicedLoanBulkCreateRequest
    {
        public List<NonKsServicedLoanCreateItem> Loans { get; init; } = [];
    }

    public sealed class NonKsServicedLoanBulkUpdateRequest
    {
        public List<NonKsServicedLoanUpdateItem> Loans { get; init; } = [];
    }
}
