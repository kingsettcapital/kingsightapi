using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

/// <summary>Legacy fund catalog (pre–Capital Data Portal).</summary>
[ApiController]
[Route("api/fund-catalog")]
public class FundCatalogController : ControllerBase
{
    private readonly IFundService _service;
    private readonly ILogger<FundCatalogController> _logger;

    public FundCatalogController(IFundService service, ILogger<FundCatalogController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET: api/fund-catalog
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
            _logger.LogInformation("Get fund catalog cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fund catalog");
            return StatusCode(500, "An error occurred while retrieving funds.");
        }
    }
}
