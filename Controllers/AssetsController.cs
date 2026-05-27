using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetsController : ControllerBase
{
    private readonly IPropertyPortalService _service;
    private readonly ILogger<AssetsController> _logger;

    public AssetsController(IPropertyPortalService service, ILogger<AssetsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET: api/assets?search=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PagedResult<PropertyListItemDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _service.GetPropertiesAsync(search, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get assets cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets");
            return StatusCode(500, "An error occurred while retrieving assets.");
        }
    }

    // GET: api/assets/{propertyKey}
    [AllowAnonymous]
    [HttpGet("{propertyKey:long}")]
    public async Task<ActionResult<PropertyDetailDto>> GetByKey(long propertyKey)
    {
        try
        {
            var result = await _service.GetPropertyByKeyAsync(propertyKey);
            return result is null ? NotFound() : Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get asset {PropertyKey} cancelled", propertyKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset {PropertyKey}", propertyKey);
            return StatusCode(500, "An error occurred while retrieving the asset.");
        }
    }

    // GET: api/assets/{propertyKey}/investments
    [AllowAnonymous]
    [HttpGet("{propertyKey:long}/investments")]
    public async Task<ActionResult<IReadOnlyList<PropertyInvestmentDto>>> GetInvestments(long propertyKey)
    {
        try
        {
            var result = await _service.GetPropertyInvestmentsAsync(propertyKey);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investments for asset {PropertyKey} cancelled", propertyKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investments for asset {PropertyKey}", propertyKey);
            return StatusCode(500, "An error occurred while retrieving asset investments.");
        }
    }
}
