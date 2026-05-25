using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services;

public sealed class LoanFormService : ILoanFormService
{
    private readonly string _connectionString;

    public LoanFormService(IConfiguration configuration, ILogger<LoanFormService> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
    }

    #region Security Value

    public Task<PagedResult<SecurityValueDto>> GetSecurityValuesAsync(LoanFormListFilter filter)
    {
        var (page, pageSize, _) = Pagination.Normalize(filter.Page, filter.PageSize);
        // TODO: SELECT collateral_per_yardi, security_value from mort.dim_loan.
        return Task.FromResult(new PagedResult<SecurityValueDto>
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        });
    }

    public Task<LoanFormBatchUpsertResult> UpsertSecurityValuesAsync(SecurityValueBatchRequest request, string? updatedBy = null)
    {
        // TODO: UPDATE security_value, user_updated_date, user_updated_by on mort.dim_loan.
        return Task.FromResult(new LoanFormBatchUpsertResult());
    }

    #endregion

    #region Other Cost Capture

    public Task<PagedResult<OtherCostCaptureDto>> GetOtherCostCaptureAsync(LoanFormListFilter filter)
    {
        var (page, pageSize, _) = Pagination.Normalize(filter.Page, filter.PageSize);
        // TODO: SELECT outstanding_invoices, est_realization_costs, cost_to_complete.
        return Task.FromResult(new PagedResult<OtherCostCaptureDto>
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        });
    }

    public Task<LoanFormBatchUpsertResult> UpsertOtherCostCaptureAsync(OtherCostCaptureBatchRequest request, string? updatedBy = null)
    {
        // TODO: Batch UPDATE other cost columns.
        return Task.FromResult(new LoanFormBatchUpsertResult());
    }

    #endregion

    #region Default Date Capture

    public Task<PagedResult<DefaultDateCaptureDto>> GetDefaultDateCaptureAsync(LoanFormListFilter filter)
    {
        var (page, pageSize, _) = Pagination.Normalize(filter.Page, filter.PageSize);
        // TODO: SELECT loan_term_default_date, default_date.
        return Task.FromResult(new PagedResult<DefaultDateCaptureDto>
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        });
    }

    public Task<LoanFormBatchUpsertResult> UpsertDefaultDateCaptureAsync(DefaultDateCaptureBatchRequest request, string? updatedBy = null)
    {
        // TODO: Batch UPDATE default_date.
        return Task.FromResult(new LoanFormBatchUpsertResult());
    }

    #endregion

    #region Subjective Analytics

    public Task<PagedResult<SubjectiveAnalyticsDto>> GetSubjectiveAnalyticsAsync(LoanFormListFilter filter)
    {
        var (page, pageSize, _) = Pagination.Normalize(filter.Page, filter.PageSize);
        // TODO: SELECT default_status, exit_plan, exit_date, maturity_additional_detail.
        return Task.FromResult(new PagedResult<SubjectiveAnalyticsDto>
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        });
    }

    public Task<LoanFormBatchUpsertResult> UpsertSubjectiveAnalyticsAsync(SubjectiveAnalyticsBatchRequest request, string? updatedBy = null)
    {
        // TODO: Batch UPDATE subjective analytics columns.
        return Task.FromResult(new LoanFormBatchUpsertResult());
    }

    #endregion

    #region Tax Arrears

    public Task<PagedResult<TaxArrearsDto>> GetTaxArrearsAsync(LoanFormListFilter filter)
    {
        var (page, pageSize, _) = Pagination.Normalize(filter.Page, filter.PageSize);
        // TODO: SELECT from tax arrears table/view joined to dim_loan.
        return Task.FromResult(new PagedResult<TaxArrearsDto>
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        });
    }

    public Task<LoanFormBatchUpsertResult> UpsertTaxArrearsAsync(TaxArrearsBatchRequest request, string? updatedBy = null)
    {
        // TODO: Batch UPDATE tax_arrears, tax_year, notes.
        return Task.FromResult(new LoanFormBatchUpsertResult());
    }

    public Task<TaxArrearsDto?> CreateTaxArrearsAsync(TaxArrearsCreateRequest request, string? updatedBy = null)
    {
        // TODO: INSERT new tax arrears row.
        return Task.FromResult<TaxArrearsDto?>(null);
    }

    #endregion

    #region LTV Validation

    public Task<PagedResult<LtvValidationDto>> GetLtvValidationAsync(LoanFormListFilter filter)
    {
        var (page, pageSize, _) = Pagination.Normalize(filter.Page, filter.PageSize);
        // TODO: SELECT parent/child loan, investor alias, LTV, exposure, ranking.
        return Task.FromResult(new PagedResult<LtvValidationDto>
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        });
    }

    public Task<LoanFormBatchUpsertResult> UpsertLtvValidationAsync(LtvValidationBatchRequest request, string? updatedBy = null)
    {
        // TODO: Batch UPDATE security_value, ltv, commentary.
        return Task.FromResult(new LoanFormBatchUpsertResult());
    }

    public Task<LoanFormBatchUpsertResult> ConfirmLtvValidationAsync(LtvValidationConfirmRequest request, string? updatedBy = null)
    {
        // TODO: Mark loans confirmed in DWH / workflow table.
        return Task.FromResult(new LoanFormBatchUpsertResult());
    }

    #endregion
}
