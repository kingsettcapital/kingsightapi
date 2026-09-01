using kingsightapi.Entities;
using kingsightapi.Services;
using kingsightapi.Configuration;
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving asset filter options");
            return StatusCode(500, "An error occurred while retrieving asset filter options.");
        }
    }

    // GET: api/assets?search=&view=ltd|quarterly&dateKey=&assetType=&investmentType=&geography=&status=&fundCode=&sortBy=&sortDir=asc|desc&page=1&pageSize=50
    [HttpGet]
    public async Task<ActionResult<PortalListPageResult<PropertyListItemDto, AssetListSummaryDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
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
        var resolvedView = view ?? TimeGranularity.Ltd;
        if (resolvedView == TimeGranularity.Quarterly && dateKey is null)
        {
            return BadRequest(
                "Query parameter 'dateKey' is required when view is quarterly (yyyyMMdd from period dropdown).");
        }

        try
        {
            var result = await _service.GetPropertiesAsync(
                search,
                assetType,
                investmentType,
                geography,
                status,
                sortBy,
                sortDir,
                page,
                pageSize,
                fundCode,
                resolvedView,
                dateKey);
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving assets");
            return StatusCode(500, "An error occurred while retrieving assets.");
        }
    }

    // GET: api/assets/{propertyKey}
    [HttpGet("{propertyKey:long}")]
    public async Task<ActionResult<PropertyProfileDto>> GetByKey(long propertyKey)
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving asset {PropertyKey}", propertyKey);
            return StatusCode(500, "An error occurred while retrieving the asset.");
        }
    }

    // GET: api/assets/{propertyKey}/leasing-summary
    [HttpGet("{propertyKey:long}/leasing-summary")]
    public async Task<ActionResult<AssetLeasingSummaryDto>> GetLeasingSummary(long propertyKey)
    {
        try
        {
            var result = await _service.GetPropertyLeasingSummaryAsync(propertyKey);
            return result is null ? NotFound() : Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get leasing summary for asset {PropertyKey} cancelled", propertyKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving leasing summary for asset {PropertyKey}", propertyKey);
            return StatusCode(500, "An error occurred while retrieving the asset leasing summary.");
        }
    }

    // GET: api/assets/{propertyKey}/fund-holdings
    [HttpGet("{propertyKey:long}/fund-holdings")]
    public async Task<ActionResult<IReadOnlyList<PropertyFundHoldingDto>>> GetFundHoldings(long propertyKey)
    {
        try
        {
            var result = await _service.GetPropertyFundHoldingsAsync(propertyKey);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get fund holdings for asset {PropertyKey} cancelled", propertyKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving fund holdings for asset {PropertyKey}", propertyKey);
            return StatusCode(500, "An error occurred while retrieving asset fund holdings.");
        }
    }

    // GET: api/assets/{propertyKey}/property-details
    [HttpGet("{propertyKey:long}/property-details")]
    public async Task<ActionResult<IReadOnlyList<AssetPropertyDetailRowDto>>> GetPropertyDetails(long propertyKey)
    {
        try
        {
            var result = await _service.GetPropertyDetailsAsync(propertyKey);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get property details for asset {PropertyKey} cancelled", propertyKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(
                _logger,
                ex,
                "Error retrieving property details for asset {PropertyKey}",
                propertyKey);
            return StatusCode(500, "An error occurred while retrieving asset property details.");
        }
    }

    // GET: api/assets/{propertyKey}/asset-type-summary
    [HttpGet("{propertyKey:long}/asset-type-summary")]
    public async Task<ActionResult<IReadOnlyList<AssetTypeSummaryRowDto>>> GetAssetTypeSummary(long propertyKey)
    {
        try
        {
            var result = await _service.GetAssetTypeSummaryAsync(propertyKey);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get asset type summary for asset {PropertyKey} cancelled", propertyKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(
                _logger,
                ex,
                "Error retrieving asset type summary for asset {PropertyKey}",
                propertyKey);
            return StatusCode(500, "An error occurred while retrieving the asset type summary.");
        }
    }

    // GET: api/assets/{propertyKey}/financial-metrics
    [HttpGet("{propertyKey:long}/financial-metrics")]
    public async Task<ActionResult<AssetFinancialMetricsDto?>> GetFinancialMetrics(long propertyKey)
    {
        try
        {
            var result = await _service.GetAssetFinancialMetricsAsync(propertyKey);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get financial metrics for asset {PropertyKey} cancelled", propertyKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(
                _logger,
                ex,
                "Error retrieving financial metrics for asset {PropertyKey}",
                propertyKey);
            return StatusCode(500, "An error occurred while retrieving asset financial metrics.");
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving investments for asset {PropertyKey}", propertyKey);
            return StatusCode(500, "An error occurred while retrieving asset investments.");
        }
    }
}
