using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvestorsController : ControllerBase
{
    private readonly IInvestorPortalService _service;
    private readonly ILogger<InvestorsController> _logger;

    public InvestorsController(IInvestorPortalService service, ILogger<InvestorsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET: api/investors?search=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PagedResult<InvestorListItemDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _service.GetInvestorsAsync(search, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investors cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investors");
            return StatusCode(500, "An error occurred while retrieving investors.");
        }
    }

    // GET: api/investors/{investorKey}
    [AllowAnonymous]
    [HttpGet("{investorKey:long}")]
    public async Task<ActionResult<InvestorDetailDto>> GetByKey(long investorKey)
    {
        try
        {
            var result = await _service.GetInvestorByKeyAsync(investorKey);
            return result is null ? NotFound() : Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investor {InvestorKey} cancelled", investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving the investor.");
        }
    }

    // GET: api/investors/{investorKey}/investments
    [AllowAnonymous]
    [HttpGet("{investorKey:long}/investments")]
    public async Task<ActionResult<IReadOnlyList<InvestorInvestmentDto>>> GetInvestments(long investorKey)
    {
        try
        {
            var result = await _service.GetInvestorInvestmentsAsync(investorKey);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investments for investor {InvestorKey} cancelled", investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investments for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving investor investments.");
        }
    }
}
