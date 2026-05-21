using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using kingsightapi.Services;
using kingsightapi.Entities;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FundsController : ControllerBase
    {
        private readonly IFundService _service;
        private readonly ILogger<FundsController> _logger;

        public FundsController(IFundService service, ILogger<FundsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET: api/funds
        [HttpGet]
        public async Task<ActionResult<List<FundDto>>> Get()
        {
            try
            {
                var result = await _service.GetFundsAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get all funds cancelled");
                return StatusCode(499); // Client Closed Request (non-standard) — indicates cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving funds");
                return StatusCode(500, "An error occurred while retrieving funds.");
            }
        }

        //// GET: api/funds/{fundKey}
        //[HttpGet("{fundKey:int}")]
        //public async Task<ActionResult<FundDto>> GetByKey(int fundKey)
        //{
        //    try
        //    {
        //        var dto = await _service.GetByKeyAsync(fundKey, cancellationToken);
        //        return dto is null ? NotFound() : Ok(dto);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.LogInformation("Get fund by key {FundKey} cancelled", fundKey);
        //        return StatusCode(499);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error retrieving fund {FundKey}", fundKey);
        //        return StatusCode(500, "An error occurred while retrieving the fund.");
        //    }
        //}
    }
}