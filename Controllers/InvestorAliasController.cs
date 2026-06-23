using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvestorAliasController : ControllerBase
    {
        private readonly IInvestorAliasService _service;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly ILogger<InvestorAliasController> _logger;

        public InvestorAliasController(
            IInvestorAliasService service,
            ICurrentUserResolver currentUserResolver,
            ILogger<InvestorAliasController> logger)
        {
            _service = service;
            _currentUserResolver = currentUserResolver;
            _logger = logger;
        }

        // GET: api/InvestorAlias
        [HttpGet]
        public async Task<ActionResult<List<InvestorAliasDto>>> GetAll()
        {
            try
            {
                var result = await _service.GetAllAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get all investor alias rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving investor alias rows");
                return StatusCode(500, "An error occurred while retrieving investor alias rows.");
            }
        }

        // GET: api/InvestorAlias/{investorAliasId}
        [HttpGet("{investorAliasId:long}")]
        public async Task<ActionResult<InvestorAliasDto>> GetById(long investorAliasId)
        {
            try
            {
                var result = await _service.GetByIdAsync(investorAliasId);
                return result is null ? NotFound() : Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get investor alias row {InvestorAliasId} cancelled", investorAliasId);
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving investor alias row {InvestorAliasId}", investorAliasId);
                return StatusCode(500, "An error occurred while retrieving the investor alias row.");
            }
        }

        // POST: api/InvestorAlias
        [HttpPost]
        public async Task<ActionResult<InvestorAliasDto>> Save(
            [FromBody] InvestorAliasSaveRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.InvestorAliasName))
            {
                return BadRequest("Investor alias name is required.");
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
                return CreatedAtAction(nameof(GetById), new { investorAliasId = newId }, created);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Save investor alias row cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving investor alias row");
                return StatusCode(500, "An error occurred while saving the investor alias row.");
            }
        }

        // PUT: api/InvestorAlias/{investorAliasId}
        [HttpPut("{investorAliasId:long}")]
        public async Task<ActionResult<InvestorAliasDto>> Update(
            long investorAliasId,
            [FromBody] InvestorAliasUpdateRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.InvestorAliasName))
            {
                return BadRequest("Investor alias name is required.");
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
                var updated = await _service.UpdateAsync(investorAliasId, request, auditDisplayName!);
                if (!updated)
                {
                    return NotFound();
                }

                var result = await _service.GetByIdAsync(investorAliasId);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update investor alias row {InvestorAliasId} cancelled", investorAliasId);
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating investor alias row {InvestorAliasId}", investorAliasId);
                return StatusCode(500, "An error occurred while updating the investor alias row.");
            }
        }

        // DELETE: api/InvestorAlias/{investorAliasId}
        [HttpDelete("{investorAliasId:long}")]
        public async Task<IActionResult> Delete(long investorAliasId)
        {
            try
            {
                var deleted = await _service.DeleteAsync(investorAliasId);
                return deleted ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Delete investor alias row {InvestorAliasId} cancelled", investorAliasId);
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting investor alias row {InvestorAliasId}", investorAliasId);
                return StatusCode(500, "An error occurred while deleting the investor alias row.");
            }
        }
    }
}
