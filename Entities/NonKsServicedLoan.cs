namespace kingsightapi.Entities
{
    public sealed class NonKsServicedLoanRowDto
    {
        public long NonKsServicedLoanKey { get; init; }
        public string? LoanName { get; init; }
        public DateTime? AsAtDate { get; init; }
        public string? LoanId { get; init; }
        public string? ServicerId { get; init; }
        public string? Description { get; init; }
        public string? Investor { get; init; }
        public DateTime? DateOfDefault { get; init; }
        public DateTime? MaturityDate { get; init; }
        public DateTime? InterestOffDate { get; init; }
        public DateTime? TaxMemoDate { get; init; }
        public decimal? SecurityValue { get; init; }
        public int? Units { get; init; }
        public decimal? NetAcres { get; init; }
        public decimal? SquareFeet { get; init; }
        public decimal? InterestRate { get; init; }
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
        public string? UserUpdatedBy { get; init; }
        public DateTime? UserUpdatedDate { get; init; }
    }

    public class NonKsServicedLoanCreateItem
    {
        public string? LoanName { get; init; }
        public DateTime? AsAtDate { get; init; }
        public string? LoanId { get; init; }
        public string? ServicerId { get; init; }
        public string? Description { get; init; }
        public string? Investor { get; init; }
        public DateTime? DateOfDefault { get; init; }
        public DateTime? MaturityDate { get; init; }
        public DateTime? InterestOffDate { get; init; }
        public DateTime? TaxMemoDate { get; init; }
        public decimal? SecurityValue { get; init; }
        public int? Units { get; init; }
        public decimal? NetAcres { get; init; }
        public decimal? SquareFeet { get; init; }
        public decimal? InterestRate { get; init; }
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
        public string UserUpdatedBy { get; init; } = "system";
    }

    public sealed class NonKsServicedLoanUpdateItem : NonKsServicedLoanCreateItem
    {
        public long NonKsServicedLoanKey { get; init; }
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
