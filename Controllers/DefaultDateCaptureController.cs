using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DefaultDateCaptureController : ControllerBase
    {
        private readonly IDefaultDateCaptureService _service;
        private readonly ILoanSecurityValueService _loanSecurityValueService;
        private readonly ILogger<DefaultDateCaptureController> _logger;

        public DefaultDateCaptureController(
            IDefaultDateCaptureService service,
            ILoanSecurityValueService loanSecurityValueService,
            ILogger<DefaultDateCaptureController> logger)
        {
            _service = service;
            _loanSecurityValueService = loanSecurityValueService;
            _logger = logger;
        }

        // GET: api/DefaultDateCapture?loanAliasIds=1&loanAliasIds=2&statuses=2
        [HttpGet]
        public async Task<ActionResult<List<DefaultDateCaptureRowDto>>> Get(
            [FromQuery] int[]? loanAliasIds,
            [FromQuery] string[]? statuses,
            CancellationToken cancellationToken)
        {
            if (loanAliasIds is null || loanAliasIds.Length == 0 || loanAliasIds.Any(id => id <= 0))
            {
                return BadRequest("At least one valid loanAliasIds value is required.");
            }

            try
            {
                var result = await _service.GetAsync(loanAliasIds, statuses, cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get default date capture rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default date capture rows");
                return StatusCode(500, "An error occurred while retrieving default date capture rows.");
            }
        }

        // GET: api/DefaultDateCapture/statuses (optional; SPA may use LoanSecurityValue/statuses)
        [HttpGet("statuses")]
        public async Task<ActionResult<List<LoanSecurityValueStatusOptionDto>>> GetStatuses(
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _loanSecurityValueService.GetStatusOptionsAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get default date capture status options cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default date capture status options");
                return StatusCode(500, "An error occurred while retrieving status filter options.");
            }
        }

        // PUT: api/DefaultDateCapture
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] DefaultDateCaptureBulkUpdateRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null || request.Loans.Count == 0)
            {
                return BadRequest("Request body must include at least one loan row.");
            }

            foreach (var loan in request.Loans)
            {
                if (loan.LoanKey <= 0)
                {
                    return BadRequest("Loan key is required.");
                }

                if (string.IsNullOrWhiteSpace(loan.UserUpdatedBy))
                {
                    return BadRequest("User updated by is required.");
                }
            }

            try
            {
                var updated = await _service.UpdateAsync(request, cancellationToken);
                return updated ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Default date capture update validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update default date capture rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating default date capture rows");
                return StatusCode(500, "An error occurred while updating default date capture rows.");
            }
        }
    }
}
