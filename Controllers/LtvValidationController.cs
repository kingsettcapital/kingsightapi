using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LtvValidationController : ControllerBase
    {
        private readonly ILtvValidationService _service;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly ILogger<LtvValidationController> _logger;

        public LtvValidationController(
            ILtvValidationService service,
            ICurrentUserResolver currentUserResolver,
            ILogger<LtvValidationController> logger)
        {
            _service = service;
            _currentUserResolver = currentUserResolver;
            _logger = logger;
        }

        // GET: api/LtvValidation?statuses=2&loanAliasIds=1
        // loanAliasIds optional — omit / empty = all aliases; statuses filter via dim_loan.funding_status_*.
        [HttpGet]
        public async Task<ActionResult<List<LtvValidationRowDto>>> Get(
            [FromQuery] int[]? loanAliasIds,
            [FromQuery] string[]? statuses,
            CancellationToken cancellationToken)
        {
            if (loanAliasIds is not null && loanAliasIds.Any(id => id <= 0))
            {
                return BadRequest("loanAliasIds must contain positive integers.");
            }

            try
            {
                var aliasFilter = loanAliasIds?.Where(id => id > 0).ToArray() ?? [];
                var result = await _service.GetAsync(aliasFilter, statuses, cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get LTV validation rows cancelled");
                return StatusCode(499);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Get LTV validation rows validation failed");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving LTV validation rows");
                return StatusCode(500, "An error occurred while retrieving LTV validation rows.");
            }
        }

        // PUT: api/LtvValidation
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] LtvValidationBulkUpdateRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null || request.Loans.Count == 0)
            {
                return BadRequest("Request body must include at least one loan row.");
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
                return updated ? Ok() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "LTV validation update validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update LTV validation rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating LTV validation rows");
                return StatusCode(500, "An error occurred while updating LTV validation rows.");
            }
        }

        // POST: api/LtvValidation/confirm
        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm(
            [FromBody] LtvValidationConfirmRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null
                || (request.LoanKeys.Count == 0 && request.LoanCodes.Count == 0))
            {
                return BadRequest("Request body must include at least one loan key or loan code.");
            }

            var (auditDisplayName, auditError) = await _currentUserResolver.RequireAuditDisplayNameAsync(
                request.UserUpdatedBy,
                "userUpdatedBy",
                cancellationToken);
            if (auditError is not null)
            {
                return auditError;
            }

            try
            {
                var confirmed = await _service.ConfirmAsync(request, auditDisplayName!, cancellationToken);
                return confirmed ? Ok() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "LTV validation confirm validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Confirm LTV validation rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming LTV validation rows");
                return StatusCode(500, "An error occurred while confirming LTV validation rows.");
            }
        }
    }
}
