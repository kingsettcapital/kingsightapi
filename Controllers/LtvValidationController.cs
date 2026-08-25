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
        private readonly IUserService _userService;
        private readonly ILogger<LtvValidationController> _logger;

        public LtvValidationController(
            ILtvValidationService service,
            ICurrentUserResolver currentUserResolver,
            IUserService userService,
            ILogger<LtvValidationController> logger)
        {
            _service = service;
            _currentUserResolver = currentUserResolver;
            _userService = userService;
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

        // GET: api/LtvValidation/column-dates
        [HttpGet("column-dates")]
        public async Task<ActionResult<LtvValidationColumnDatesDto>> GetColumnDates(
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.GetColumnDatesAsync(cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get LTV column dates cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving LTV column dates");
                return StatusCode(500, "An error occurred while retrieving LTV column dates.");
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

            var approverError = await _currentUserResolver.RequireMortgageApproverAsync(
                _userService,
                cancellationToken);
            if (approverError is not null)
            {
                return approverError;
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

        // POST: api/LtvValidation/confirm — locks LTV (is_confirmed = 'Y')
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

            var approverError = await _currentUserResolver.RequireMortgageApproverAsync(
                _userService,
                cancellationToken);
            if (approverError is not null)
            {
                return approverError;
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
                _logger.LogWarning(ex, "LTV validation lock validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Lock LTV validation rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking LTV validation rows");
                return StatusCode(500, "An error occurred while locking LTV validation rows.");
            }
        }

        // POST: api/LtvValidation/unlock — unlocks LTV (is_confirmed = 'N')
        [HttpPost("unlock")]
        public async Task<IActionResult> Unlock(
            [FromBody] LtvValidationUnlockRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null
                || (request.LoanKeys.Count == 0 && request.LoanCodes.Count == 0))
            {
                return BadRequest("Request body must include at least one loan key or loan code.");
            }

            var approverError = await _currentUserResolver.RequireMortgageApproverAsync(
                _userService,
                cancellationToken);
            if (approverError is not null)
            {
                return approverError;
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
                var unlocked = await _service.UnlockAsync(request, auditDisplayName!, cancellationToken);
                return unlocked ? Ok() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "LTV validation unlock validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Unlock LTV validation rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking LTV validation rows");
                return StatusCode(500, "An error occurred while unlocking LTV validation rows.");
            }
        }
    }
}
