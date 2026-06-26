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
        private readonly ILogger<LoansController> _logger;

        public LoansController(
            ILoanService service,
            ICurrentUserResolver currentUserResolver,
            ILogger<LoansController> logger)
        {
            _service = service;
            _currentUserResolver = currentUserResolver;
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

        // GET: api/Loans
        [HttpGet]
        public async Task<ActionResult<List<LoanDto>>> GetAll()
        {
            try
            {
                var result = await _service.GetAllAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get all loan rows cancelled");
                return StatusCode(499);
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
