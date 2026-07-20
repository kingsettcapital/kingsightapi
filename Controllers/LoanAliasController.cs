using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoanAliasController : ControllerBase
    {
        private readonly ILoanAliasService _service;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly ILogger<LoanAliasController> _logger;

        public LoanAliasController(
            ILoanAliasService service,
            ICurrentUserResolver currentUserResolver,
            ILogger<LoanAliasController> logger)
        {
            _service = service;
            _currentUserResolver = currentUserResolver;
            _logger = logger;
        }

        // GET: api/LoanAlias
        [HttpGet]
        public async Task<ActionResult<List<LoanAliasDto>>> GetAll()
        {
            try
            {
                var result = await _service.GetAllAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get all loan alias rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving loan alias rows");
                return StatusCode(500, "An error occurred while retrieving loan alias rows.");
            }
        }

        // GET: api/LoanAlias/{loanAliasId}
        [HttpGet("{loanAliasId:long}")]
        public async Task<ActionResult<LoanAliasDto>> GetById(long loanAliasId)
        {
            try
            {
                var result = await _service.GetByIdAsync(loanAliasId);
                return result is null ? NotFound() : Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get loan alias row {LoanAliasId} cancelled", loanAliasId);
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving loan alias row {LoanAliasId}", loanAliasId);
                return StatusCode(500, "An error occurred while retrieving the loan alias row.");
            }
        }

        // POST: api/LoanAlias
        [HttpPost]
        public async Task<ActionResult<LoanAliasDto>> Save(
            [FromBody] LoanAliasSaveRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.LoanAliasName))
            {
                return BadRequest("Loan alias name is required.");
            }

            var (auditDisplayName, auditError) = await _currentUserResolver.RequireAuditDisplayNameAsync(
                request.CreatedBy,
                "createdBy",
                cancellationToken);
            if (auditError is not null)
            {
                return auditError;
            }

            try
            {
                var newId = await _service.SaveAsync(request, auditDisplayName!);
                var created = await _service.GetByIdAsync(newId);
                return CreatedAtAction(nameof(GetById), new { loanAliasId = newId }, created);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Save loan alias row cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving loan alias row");
                return StatusCode(500, "An error occurred while saving the loan alias row.");
            }
        }

        // PUT: api/LoanAlias/{loanAliasId}
        [HttpPut("{loanAliasId:long}")]
        public async Task<ActionResult<LoanAliasDto>> Update(
            long loanAliasId,
            [FromBody] LoanAliasUpdateRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.LoanAliasName))
            {
                return BadRequest("Loan alias name is required.");
            }

            var (auditDisplayName, auditError) = await _currentUserResolver.RequireAuditDisplayNameAsync(
                request.UpdatedBy,
                "updatedBy",
                cancellationToken);
            if (auditError is not null)
            {
                return auditError;
            }

            try
            {
                var updated = await _service.UpdateAsync(loanAliasId, request, auditDisplayName!);
                if (!updated)
                {
                    return NotFound();
                }

                var result = await _service.GetByIdAsync(loanAliasId);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update loan alias row {LoanAliasId} cancelled", loanAliasId);
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating loan alias row {LoanAliasId}", loanAliasId);
                return StatusCode(500, "An error occurred while updating the loan alias row.");
            }
        }

        // DELETE: api/LoanAlias/{loanAliasId}
        [HttpDelete("{loanAliasId:long}")]
        public async Task<IActionResult> Delete(long loanAliasId)
        {
            try
            {
                var deleted = await _service.DeleteAsync(loanAliasId);
                return deleted ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Delete loan alias row {LoanAliasId} cancelled", loanAliasId);
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting loan alias row {LoanAliasId}", loanAliasId);
                return StatusCode(500, "An error occurred while deleting the loan alias row.");
            }
        }
    }
}
