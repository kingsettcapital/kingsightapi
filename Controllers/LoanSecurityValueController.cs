using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoanSecurityValueController : ControllerBase
    {
        private readonly ILoanSecurityValueService _service;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly ILogger<LoanSecurityValueController> _logger;

        public LoanSecurityValueController(
            ILoanSecurityValueService service,
            ICurrentUserResolver currentUserResolver,
            ILogger<LoanSecurityValueController> logger)
        {
            _service = service;
            _currentUserResolver = currentUserResolver;
            _logger = logger;
        }

        // GET: api/LoanSecurityValue/statuses
        [HttpGet("statuses")]
        public async Task<ActionResult<List<LoanSecurityValueStatusOptionDto>>> GetStatuses()
        {
            try
            {
                var result = await _service.GetStatusOptionsAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get loan security value status options cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving loan security value status options");
                return StatusCode(500, "An error occurred while retrieving status filter options.");
            }
        }

        // GET: api/LoanSecurityValue?loanAliasIds=1&statuses=3&statuses=In%20Default
        // statuses: wh_gold1.shared.dim_status.status_key (numeric) or status_name;
        // applied via shared.dim_loan.funding_status_code; (null) = no funding status on dim_loan
        [HttpGet]
        public async Task<ActionResult<List<LoanSecurityValueDto>>> GetAll(
            [FromQuery] long[]? loanAliasIds,
            [FromQuery] string[]? statuses)
        {
            try
            {
                var result = await _service.GetAllAsync(loanAliasIds, statuses);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get loan security value rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving loan security value rows");
                return StatusCode(500, "An error occurred while retrieving loan security value rows.");
            }
        }

        // PUT: api/LoanSecurityValue
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] LoanSecurityValueBatchUpdateRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null || request.LoanSecurityValues.Count == 0)
            {
                return BadRequest("Request body must include at least one loan security value row.");
            }

            foreach (var item in request.LoanSecurityValues)
            {
                if (item.LoanAliasId <= 0)
                {
                    return BadRequest("Loan alias id is required.");
                }
            }

            var clientAudit = request.LoanSecurityValues.FirstOrDefault()?.UpdatedBy;
            var (auditDisplayName, auditError) = await _currentUserResolver.RequireAuditDisplayNameAsync(
                clientAudit,
                "updatedBy",
                cancellationToken);
            if (auditError is not null)
            {
                return auditError;
            }

            try
            {
                var updated = await _service.UpdateAsync(request, auditDisplayName!);
                return updated ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update loan security value rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating loan security value rows");
                return StatusCode(500, "An error occurred while updating loan security value rows.");
            }
        }
    }
}
