namespace kingsightapi.Entities;

#region Shared

/// <summary>
/// Shared list filters for loan form screens (loan alias, status, pagination).
/// </summary>
public sealed class LoanFormListFilter
{
    public List<string>? LoanAliases { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// Batch upsert outcome for loan form saves.
/// </summary>
public sealed class LoanFormBatchUpsertResult
{
    public int UpdatedCount { get; init; }
    public int InsertedCount { get; init; }
    public IReadOnlyList<string> FailedLoanKeys { get; init; } = [];
}

#endregion

#region Security Value

public sealed class SecurityValueDto
{
    public long LoanKey { get; init; }
    public string LoanCode { get; init; } = string.Empty;
    public string LoanAlias { get; init; } = string.Empty;
    public decimal? CollateralPerYardi { get; init; }
    public decimal? SecurityValue { get; init; }
    public DateTime? DwhUpdateDate { get; init; }
}

public sealed class SecurityValueUpdateItem
{
    public long LoanKey { get; init; }
    public string? LoanCode { get; init; }
    public decimal? SecurityValue { get; init; }
}

public sealed class SecurityValueBatchRequest
{
    public List<SecurityValueUpdateItem> Items { get; init; } = [];
}

#endregion

#region Other Cost Capture

public sealed class OtherCostCaptureDto
{
    public long LoanKey { get; init; }
    public string LoanId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LoanAlias { get; init; } = string.Empty;
    public decimal? OutstandingInvoices { get; init; }
    public decimal? EstRealizationCosts { get; init; }
    public decimal? CostToComplete { get; init; }
    public DateTime? DwhUpdateDate { get; init; }
}

public sealed class OtherCostCaptureUpdateItem
{
    public long LoanKey { get; init; }
    public string? LoanCode { get; init; }
    public decimal? OutstandingInvoices { get; init; }
    public decimal? EstRealizationCosts { get; init; }
    public decimal? CostToComplete { get; init; }
}

public sealed class OtherCostCaptureBatchRequest
{
    public List<OtherCostCaptureUpdateItem> Items { get; init; } = [];
}

#endregion

#region Default Date Capture

public sealed class DefaultDateCaptureDto
{
    public long LoanKey { get; init; }
    public string LoanId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LoanAlias { get; init; } = string.Empty;
    public DateTime? LoanTermDefaultDate { get; init; }
    public DateTime? DefaultDate { get; init; }
    public DateTime? DwhUpdateDate { get; init; }
}

public sealed class DefaultDateCaptureUpdateItem
{
    public long LoanKey { get; init; }
    public string? LoanCode { get; init; }
    public DateTime? DefaultDate { get; init; }
}

public sealed class DefaultDateCaptureBatchRequest
{
    public List<DefaultDateCaptureUpdateItem> Items { get; init; } = [];
}

#endregion

#region Subjective Analytics

public sealed class SubjectiveAnalyticsDto
{
    public long LoanKey { get; init; }
    public string LoanId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LoanAlias { get; init; } = string.Empty;
    public DateTime? MaturityDate { get; init; }
    public string DefaultStatus { get; init; } = string.Empty;
    public string ExitPlan { get; init; } = string.Empty;
    public string ExitDate { get; init; } = string.Empty;
    public string MaturityAdditionalDetail { get; init; } = string.Empty;
    public DateTime? DwhUpdateDate { get; init; }
}

public sealed class SubjectiveAnalyticsUpdateItem
{
    public long LoanKey { get; init; }
    public string? LoanCode { get; init; }
    public string? DefaultStatus { get; init; }
    public string? ExitPlan { get; init; }
    public string? ExitDate { get; init; }
    public string? MaturityAdditionalDetail { get; init; }
}

public sealed class SubjectiveAnalyticsBatchRequest
{
    public List<SubjectiveAnalyticsUpdateItem> Items { get; init; } = [];
}

#endregion

#region Tax Arrears

public sealed class TaxArrearsDto
{
    public long LoanKey { get; init; }
    public string LoanId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LoanAlias { get; init; } = string.Empty;
    public DateTime? TaxMemoDate { get; init; }
    public decimal? TaxArrears { get; init; }
    public int? TaxYear { get; init; }
    public string Notes { get; init; } = string.Empty;
    public DateTime? DwhUpdateDate { get; init; }
}

public sealed class TaxArrearsUpdateItem
{
    public long LoanKey { get; init; }
    public string? LoanCode { get; init; }
    public decimal? TaxArrears { get; init; }
    public int? TaxYear { get; init; }
    public string? Notes { get; init; }
}

public sealed class TaxArrearsBatchRequest
{
    public List<TaxArrearsUpdateItem> Items { get; init; } = [];
}

public sealed class TaxArrearsCreateRequest
{
    public string LoanAlias { get; init; } = string.Empty;
    public string? LoanCode { get; init; }
    public string? SyndicateId { get; init; }
    public string? SyndicateDescription { get; init; }
    public DateTime? TaxMemoDate { get; init; }
    public decimal? TaxArrears { get; init; }
    public int? TaxYear { get; init; }
    public string? Notes { get; init; }
}

#endregion

#region LTV Validation

public sealed class LtvValidationDto
{
    public long LoanKey { get; init; }
    public string ParentLoanId { get; init; } = string.Empty;
    public string ChildLoanId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LoanAlias { get; init; } = string.Empty;
    public string InvestorAlias { get; init; } = string.Empty;
    public decimal? SecurityValue { get; init; }
    public decimal? Exposure { get; init; }
    public short? Ranking { get; init; }
    public decimal? Ltv { get; init; }
    public string Commentary { get; init; } = string.Empty;
    public DateTime? DwhUpdateDate { get; init; }
}

public sealed class LtvValidationUpdateItem
{
    public long LoanKey { get; init; }
    public string? LoanCode { get; init; }
    public decimal? SecurityValue { get; init; }
    public decimal? Ltv { get; init; }
    public string? Commentary { get; init; }
}

public sealed class LtvValidationBatchRequest
{
    public List<LtvValidationUpdateItem> Items { get; init; } = [];
}

public sealed class LtvValidationConfirmRequest
{
    public List<long> LoanKeys { get; init; } = [];
}

#endregion
