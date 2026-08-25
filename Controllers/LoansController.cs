using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoansController : ControllerBase
    {
        private readonly ILoanService _service;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly IUserService _userService;
        private readonly ILogger<LoansController> _logger;

        public LoansController(
            ILoanService service,
            ICurrentUserResolver currentUserResolver,
            IUserService userService,
            ILogger<LoansController> logger)
        {
            _service = service;
            _currentUserResolver = currentUserResolver;
            _userService = userService;
            _logger = logger;
        }

        // GET: api/Loans/lookups (loan alias dropdown from loan_alias_master)
        [HttpGet("lookups")]
        public async Task<ActionResult<LoanLookupsDto>> GetLookups(CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _service.GetLookupsAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get loan lookups cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving loan lookups");
                return StatusCode(500, "An error occurred while retrieving loan lookups.");
            }
        }

        // GET: api/Loans?auditProfile=loan_alias|loan_attribute&statuses=2
        [HttpGet]
        public async Task<ActionResult<List<LoanDto>>> GetAll(
            [FromQuery] string? auditProfile = null,
            [FromQuery] List<string>? statuses = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _service.GetAllAsync(auditProfile, statuses, cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get all loan rows cancelled");
                return StatusCode(499);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Get all loan rows validation failed");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving loan rows");
                return StatusCode(500, "An error occurred while retrieving loan rows.");
            }
        }

        // PUT: api/Loans
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] LoanUpdateBatchRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            foreach (var loan in request.Loans)
            {
                if (!loan.LoanAliasKey.HasValue)
                {
                    return BadRequest("Loan alias key is required.");
                }
            }

            var superUserError = await _currentUserResolver.RequireMortgageSuperUserAsync(
                _userService,
                cancellationToken);
            if (superUserError is not null)
            {
                return superUserError;
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
                var updated = await _service.UpdateAsync(request, auditDisplayName!);
                return updated ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update loan rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating loan rows");
                return StatusCode(500, "An error occurred while updating loan rows.");
            }
        }
    }
}
