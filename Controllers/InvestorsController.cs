using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvestorsController : ControllerBase
    {
        private readonly IInvestorService _service;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly ILogger<InvestorsController> _logger;

        public InvestorsController(
            IInvestorService service,
            ICurrentUserResolver currentUserResolver,
            ILogger<InvestorsController> logger)
        {
            _service = service;
            _currentUserResolver = currentUserResolver;
            _logger = logger;
        }

        // GET: api/Investors
        [HttpGet]
        public async Task<ActionResult<List<InvestorDto>>> GetAll()
        {
            try
            {
                var result = await _service.GetAllAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get all investor rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving investor rows");
                return StatusCode(500, "An error occurred while retrieving investor rows.");
            }
        }

        // PUT: api/Investors
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] InvestorUpdateBatchRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            foreach (var investor in request.Investors)
            {
                if (!investor.InvestorAliasKey.HasValue)
                {
                    return BadRequest("Investor alias key is required.");
                }

                if (string.IsNullOrWhiteSpace(investor.InvestorCode))
                {
                    return BadRequest("Investor code is required.");
                }
            }

            var clientAudit = request.Investors.FirstOrDefault()?.UserUpdatedBy;
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
                _logger.LogInformation("Update investor cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating investor row");
                return StatusCode(500, "An error occurred while updating the investor row.");
            }
        }
    }
}
