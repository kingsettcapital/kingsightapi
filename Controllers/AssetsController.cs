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
    private readonly IPortalFilterService _filterService;
    private readonly ILogger<AssetsController> _logger;

    public AssetsController(
        IPropertyPortalService service,
        IPortalFilterService filterService,
        ILogger<AssetsController> logger)
    {
        _service = service;
        _filterService = filterService;
        _logger = logger;
    }

    // GET: api/assets/filter-options
    [HttpGet("filter-options")]
    public async Task<ActionResult<AssetListFilterOptionsDto>> GetFilterOptions()
    {
        try
        {
            return Ok(await _filterService.GetAssetListFilterOptionsAsync());
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get asset filter options cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset filter options");
            return StatusCode(500, "An error occurred while retrieving asset filter options.");
        }
    }

    // GET: api/assets?search=&assetType=&investmentType=&geography=&status=&fundCode=&sortBy=&sortDir=asc|desc&page=1&pageSize=50
    [HttpGet]
    public async Task<ActionResult<PortalListPageResult<PropertyListItemDto, AssetListSummaryDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? assetType,
        [FromQuery] string? investmentType,
        [FromQuery] string? geography,
        [FromQuery] string? status,
        [FromQuery] string? fundCode,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _service.GetPropertiesAsync(
                search, assetType, investmentType, geography, status, sortBy, sortDir, page, pageSize, fundCode);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
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
