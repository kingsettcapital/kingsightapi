using System.Security.Claims;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvestorController : ControllerBase
    {
        private readonly IInvestorService _service;
        private readonly ILogger<InvestorController> _logger;

        public InvestorController(IInvestorService service, ILogger<InvestorController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET: api/investor/names — options for the Investor Name filter dropdown
        [HttpGet("names")]
        public async Task<ActionResult<List<InvestorNameOptionDto>>> GetInvestorNames()
        {
            try
            {
                var result = await _service.GetInvestorNameOptionsAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get investor names cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving investor names");
                return StatusCode(500, "An error occurred while retrieving investor names.");
            }
        }

        // GET: api/investor?investorNames=...&page=1&pageSize=50
        [HttpGet]
        public async Task<ActionResult<PagedResult<DimInvestorDto>>> GetAll(
            [FromQuery] List<string>? investorNames,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var result = await _service.GetInvestorsAsync(investorNames, page, pageSize);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get all investors cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving investors");
                return StatusCode(500, "An error occurred while retrieving investors.");
            }
        }

        // PUT: api/investor/alias or api/investor/aliases — batch upsert (update by key/code, insert when missing)
        [HttpPut("alias")]
        [HttpPut("aliases")]
        public async Task<ActionResult<DimInvestorAliasBatchUpdateResult>> PutAliases([FromBody] DimInvestorAliasBatchUpdateRequest request)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            if (request.Investors is null || request.Investors.Count == 0)
            {
                return BadRequest("At least one investor is required.");
            }

            try
            {
                var result = await _service.UpdateInvestorAliasesAsync(request, GetCurrentUser());
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Batch update investor aliases cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch updating investor aliases");
                return StatusCode(500, "An error occurred while updating investor aliases.");
            }
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("preferred_username")
            ?? User.FindFirstValue(ClaimTypes.Upn)
            ?? User.Identity?.Name
            ?? "System";
    }
}
