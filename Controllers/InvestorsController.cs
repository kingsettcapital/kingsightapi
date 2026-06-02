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
        private readonly ILogger<InvestorsController> _logger;

        public InvestorsController(
            IInvestorService service,
            ILogger<InvestorsController> logger)
        {
            _service = service;
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

        // PUT: api/Investors/{investorKey}
        //[HttpPut("{investorKey:long}")]
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] InvestorUpdateBatchRequest request)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }
            foreach (var investor in request.Investors)
            {
                if (!investor.InvestorAliasKey.HasValue)
                {
                    return BadRequest("Investor alias name is required.");
                }
            }
                

            try
            {
                var updated = await _service.UpdateAsync(request);
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
