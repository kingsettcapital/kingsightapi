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

        // GET: api/investor
        [HttpGet]
        public async Task<ActionResult<List<DimInvestorDto>>> GetAll()
        {
            try
            {
                var result = await _service.GetInvestorsAsync();
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

        // PUT: api/investor/alias or api/investor/aliases (Angular often uses plural)
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
                return BadRequest("At least one investor key and alias name is required.");
            }

            try
            {
                var result = await _service.UpdateInvestorAliasesAsync(request);
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
    }
}
