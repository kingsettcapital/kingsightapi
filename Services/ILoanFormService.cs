using kingsightapi.Entities;

namespace kingsightapi.Services;

public interface ILoanFormService
{
  
    // Form: Security Value
    Task<PagedResult<SecurityValueDto>> GetSecurityValuesAsync(LoanFormListFilter filter);
    Task<LoanFormBatchUpsertResult> UpsertSecurityValuesAsync(SecurityValueBatchRequest request, string? updatedBy = null);

    // Form: Other Cost Capture
    Task<PagedResult<OtherCostCaptureDto>> GetOtherCostCaptureAsync(LoanFormListFilter filter);
    Task<LoanFormBatchUpsertResult> UpsertOtherCostCaptureAsync(OtherCostCaptureBatchRequest request, string? updatedBy = null);

    // Form: Default Date Capture
    Task<PagedResult<DefaultDateCaptureDto>> GetDefaultDateCaptureAsync(LoanFormListFilter filter);
    Task<LoanFormBatchUpsertResult> UpsertDefaultDateCaptureAsync(DefaultDateCaptureBatchRequest request, string? updatedBy = null);

    // Form: Subjective Analytics
    Task<PagedResult<SubjectiveAnalyticsDto>> GetSubjectiveAnalyticsAsync(LoanFormListFilter filter);
    Task<LoanFormBatchUpsertResult> UpsertSubjectiveAnalyticsAsync(SubjectiveAnalyticsBatchRequest request, string? updatedBy = null);

    // Form: Tax Arrears
    Task<PagedResult<TaxArrearsDto>> GetTaxArrearsAsync(LoanFormListFilter filter);
    Task<LoanFormBatchUpsertResult> UpsertTaxArrearsAsync(TaxArrearsBatchRequest request, string? updatedBy = null);
    Task<TaxArrearsDto?> CreateTaxArrearsAsync(TaxArrearsCreateRequest request, string? updatedBy = null);

    // Form: LTV Validation
    Task<PagedResult<LtvValidationDto>> GetLtvValidationAsync(LoanFormListFilter filter);
    Task<LoanFormBatchUpsertResult> UpsertLtvValidationAsync(LtvValidationBatchRequest request, string? updatedBy = null);
    Task<LoanFormBatchUpsertResult> ConfirmLtvValidationAsync(LtvValidationConfirmRequest request, string? updatedBy = null);

}
