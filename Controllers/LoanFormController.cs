using System.Security.Claims;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

/// <summary>
/// Loan form screens. Loan syndicate: <see cref="LoansController"/> | Investor alias: <see cref="InvestorController"/>.
/// </summary>
[ApiController]
[Route("api/loan-form")]
public class LoanFormController : ControllerBase
{
    private readonly ILoanFormService _service;
    private readonly ILogger<LoanFormController> _logger;

    public LoanFormController(ILoanFormService service, ILogger<LoanFormController> logger)
    {
        _service = service;
        _logger = logger;
    }

    #region Security Value

    [HttpGet("security-values")]
    public async Task<ActionResult<PagedResult<SecurityValueDto>>> GetSecurityValues(
        [FromQuery] List<string>? loanAliases,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetPaged(_service.GetSecurityValuesAsync, loanAliases, status, page, pageSize, "security values");
    }

    [HttpPut("security-values")]
    public async Task<ActionResult<LoanFormBatchUpsertResult>> PutSecurityValues([FromBody] SecurityValueBatchRequest request)
    {
        return await PutBatch(
            request.Items.Count,
            () => _service.UpsertSecurityValuesAsync(request, GetCurrentUser()),
            "security values");
    }

    #endregion

    #region Other Cost Capture

    [HttpGet("other-costs")]
    public async Task<ActionResult<PagedResult<OtherCostCaptureDto>>> GetOtherCosts(
        [FromQuery] List<string>? loanAliases,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetPaged(_service.GetOtherCostCaptureAsync, loanAliases, status, page, pageSize, "other cost capture");
    }

    [HttpPut("other-costs")]
    public async Task<ActionResult<LoanFormBatchUpsertResult>> PutOtherCosts([FromBody] OtherCostCaptureBatchRequest request)
    {
        return await PutBatch(
            request.Items.Count,
            () => _service.UpsertOtherCostCaptureAsync(request, GetCurrentUser()),
            "other cost capture");
    }

    #endregion

    #region Default Date Capture

    [HttpGet("default-dates")]
    public async Task<ActionResult<PagedResult<DefaultDateCaptureDto>>> GetDefaultDates(
        [FromQuery] List<string>? loanAliases,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetPaged(_service.GetDefaultDateCaptureAsync, loanAliases, status, page, pageSize, "default date capture");
    }

    [HttpPut("default-dates")]
    public async Task<ActionResult<LoanFormBatchUpsertResult>> PutDefaultDates([FromBody] DefaultDateCaptureBatchRequest request)
    {
        return await PutBatch(
            request.Items.Count,
            () => _service.UpsertDefaultDateCaptureAsync(request, GetCurrentUser()),
            "default date capture");
    }

    #endregion

    #region Subjective Analytics

    [HttpGet("subjective-analytics")]
    public async Task<ActionResult<PagedResult<SubjectiveAnalyticsDto>>> GetSubjectiveAnalytics(
        [FromQuery] List<string>? loanAliases,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetPaged(_service.GetSubjectiveAnalyticsAsync, loanAliases, status, page, pageSize, "subjective analytics");
    }

    [HttpPut("subjective-analytics")]
    public async Task<ActionResult<LoanFormBatchUpsertResult>> PutSubjectiveAnalytics([FromBody] SubjectiveAnalyticsBatchRequest request)
    {
        return await PutBatch(
            request.Items.Count,
            () => _service.UpsertSubjectiveAnalyticsAsync(request, GetCurrentUser()),
            "subjective analytics");
    }

    #endregion

    #region Tax Arrears

    [HttpGet("tax-arrears")]
    public async Task<ActionResult<PagedResult<TaxArrearsDto>>> GetTaxArrears(
        [FromQuery] List<string>? loanAliases,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetPaged(_service.GetTaxArrearsAsync, loanAliases, status, page, pageSize, "tax arrears");
    }

    [HttpPut("tax-arrears")]
    public async Task<ActionResult<LoanFormBatchUpsertResult>> PutTaxArrears([FromBody] TaxArrearsBatchRequest request)
    {
        return await PutBatch(
            request.Items.Count,
            () => _service.UpsertTaxArrearsAsync(request, GetCurrentUser()),
            "tax arrears");
    }

    [HttpPost("tax-arrears")]
    public async Task<ActionResult<TaxArrearsDto>> PostTaxArrears([FromBody] TaxArrearsCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LoanAlias))
        {
            return BadRequest("Loan alias is required.");
        }

        try
        {
            var created = await _service.CreateTaxArrearsAsync(request, GetCurrentUser());
            if (created is null)
            {
                return StatusCode(501, "Create not implemented until DB schema is ready.");
            }

            return Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tax arrears");
            return StatusCode(500, "An error occurred while creating tax arrears.");
        }
    }

    #endregion

    #region LTV Validation

    [HttpGet("ltv-validation")]
    public async Task<ActionResult<PagedResult<LtvValidationDto>>> GetLtvValidation(
        [FromQuery] List<string>? loanAliases,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return await GetPaged(_service.GetLtvValidationAsync, loanAliases, status, page, pageSize, "LTV validation");
    }

    [HttpPut("ltv-validation")]
    public async Task<ActionResult<LoanFormBatchUpsertResult>> PutLtvValidation([FromBody] LtvValidationBatchRequest request)
    {
        return await PutBatch(
            request.Items.Count,
            () => _service.UpsertLtvValidationAsync(request, GetCurrentUser()),
            "LTV validation");
    }

    [HttpPost("ltv-validation/confirm")]
    public async Task<ActionResult<LoanFormBatchUpsertResult>> PostLtvValidationConfirm([FromBody] LtvValidationConfirmRequest request)
    {
        return await PutBatch(
            request.LoanKeys.Count,
            () => _service.ConfirmLtvValidationAsync(request, GetCurrentUser()),
            "LTV validation confirm");
    }

    #endregion

    #region Private helpers

    private async Task<ActionResult<PagedResult<T>>> GetPaged<T>(
        Func<LoanFormListFilter, Task<PagedResult<T>>> get,
        List<string>? loanAliases,
        string? status,
        int page,
        int pageSize,
        string label)
    {
        try
        {
            var filter = new LoanFormListFilter
            {
                LoanAliases = loanAliases,
                Status = status,
                Page = page,
                PageSize = pageSize
            };
            return Ok(await get(filter));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {Label}", label);
            return StatusCode(500, $"An error occurred while retrieving {label}.");
        }
    }

    private async Task<ActionResult<LoanFormBatchUpsertResult>> PutBatch(
        int count,
        Func<Task<LoanFormBatchUpsertResult>> upsert,
        string label)
    {
        if (count == 0)
        {
            return BadRequest("At least one item is required.");
        }

        try
        {
            return Ok(await upsert());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting {Label}", label);
            return StatusCode(500, $"An error occurred while upserting {label}.");
        }
    }

    private string GetCurrentUser()
    {
        return User.FindFirstValue("preferred_username")
            ?? User.FindFirstValue(ClaimTypes.Upn)
            ?? User.Identity?.Name
            ?? "System";
    }

    #endregion
}
