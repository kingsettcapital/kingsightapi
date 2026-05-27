using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FundsController : ControllerBase
{
    private readonly IFundPortalService _service;
    private readonly ILogger<FundsController> _logger;

    public FundsController(IFundPortalService service, ILogger<FundsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET: api/funds?search=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PagedResult<FundListItemDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _service.GetFundsAsync(search, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get funds cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving funds");
            return StatusCode(500, "An error occurred while retrieving funds.");
        }
    }

    // GET: api/funds/{fundKey}
    [AllowAnonymous]
    [HttpGet("{fundKey:int}")]
    public async Task<ActionResult<FundDetailDto>> GetByKey(int fundKey)
    {
        try
        {
            var result = await _service.GetFundByKeyAsync(fundKey);
            return result is null ? NotFound() : Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get fund {FundKey} cancelled", fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving the fund.");
        }
    }

    // GET: api/funds/{fundKey}/investors
    [AllowAnonymous]
    [HttpGet("{fundKey:int}/investors")]
    public async Task<ActionResult<IReadOnlyList<FundInvestorDto>>> GetInvestors(int fundKey)
    {
        try
        {
            var result = await _service.GetFundInvestorsAsync(fundKey);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investors for fund {FundKey} cancelled", fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investors for fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving fund investors.");
        }
    }
}
