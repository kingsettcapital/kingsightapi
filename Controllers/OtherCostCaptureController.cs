using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OtherCostCaptureController : ControllerBase
    {
        private readonly IOtherCostCaptureService _service;
        private readonly ILoanSecurityValueService _loanSecurityValueService;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly ILogger<OtherCostCaptureController> _logger;

        public OtherCostCaptureController(
            IOtherCostCaptureService service,
            ILoanSecurityValueService loanSecurityValueService,
            ICurrentUserResolver currentUserResolver,
            ILogger<OtherCostCaptureController> logger)
        {
            _service = service;
            _loanSecurityValueService = loanSecurityValueService;
            _currentUserResolver = currentUserResolver;
            _logger = logger;
        }

        // GET: api/OtherCostCapture?loanAliasId=1&statuses=2
        [HttpGet]
        public async Task<ActionResult<List<OtherCostCaptureDto>>> Get(
            [FromQuery] int loanAliasId,
            [FromQuery] string[]? statuses,
            CancellationToken cancellationToken)
        {
            if (loanAliasId <= 0)
            {
                return BadRequest("loanAliasId is required.");
            }

            try
            {
                var result = await _service.GetAsync(loanAliasId, statuses, cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get other cost capture rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving other cost capture rows for loan alias {LoanAliasId}", loanAliasId);
                return StatusCode(500, "An error occurred while retrieving other cost capture rows.");
            }
        }

        // GET: api/OtherCostCapture/statuses (optional; SPA may use LoanSecurityValue/statuses)
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
                _logger.LogInformation("Get other cost capture status options cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving other cost capture status options");
                return StatusCode(500, "An error occurred while retrieving status filter options.");
            }
        }

        // PUT: api/OtherCostCapture
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] OtherCostCaptureBatchUpdateRequest request,
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
            }

            var clientAudit = request.Loans.FirstOrDefault()?.UserUpdatedBy;
            var (auditDisplayName, auditError) = await _currentUserResolver.RequireAuditDisplayNameAsync(
                clientAudit,
                "userUpdatedBy",
                cancellationToken);
            if (auditError is not null)
            {
                return auditError;
            }

            try
            {
                var updated = await _service.UpdateAsync(request, auditDisplayName!, cancellationToken);
                return updated ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update other cost capture rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating other cost capture rows");
                return StatusCode(500, "An error occurred while updating other cost capture rows.");
            }
        }
    }
}
