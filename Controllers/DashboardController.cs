using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

/// <summary>
/// Module dashboard — section catalog and lazy-loaded section payloads.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardService service, ILogger<DashboardController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET: api/dashboard/modules/investors/sections
    [AllowAnonymous]
    [HttpGet("modules/{module}/sections")]
    public ActionResult<IReadOnlyList<DashboardSectionDefinitionDto>> GetModuleSections(string module)
    {
        if (!DashboardModules.TryParseFromApi(module, out var dashboardModule))
        {
            return BadRequest($"Invalid module '{module}'. Valid values: {DashboardModules.QueryValues}.");
        }

        return Ok(_service.GetModuleSections(dashboardModule));
    }

    // GET: api/dashboard/sections/investors-kpi-summary?view=ltd|quarterly|daily
    [AllowAnonymous]
    [HttpGet("sections/{sectionId}")]
    public async Task<ActionResult<DashboardSectionDataDto>> GetSectionData(
        string sectionId,
        [FromQuery] TimeGranularity? view)
    {
        if (!DashboardSectionIds.TryParseFromApi(sectionId, out var parsedSectionId))
        {
            return NotFound();
        }

        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        try
        {
            var result = await _service.GetSectionDataAsync(parsedSectionId, view.Value);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get dashboard section {SectionId} cancelled", sectionId);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard section {SectionId}", sectionId);
            return StatusCode(500, "An error occurred while retrieving dashboard section data.");
        }
    }

    // GET: api/dashboard/modules/investors/transactions?view=ltd|quarterly|daily&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("modules/investors/transactions")]
    public async Task<ActionResult<PagedResult<DashboardTransactionDto>>> GetInvestorTransactions(
        [FromQuery] TimeGranularity? view,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        try
        {
            var result = await _service.GetInvestorTransactionsAsync(view.Value, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investor transactions cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investor transactions");
            return StatusCode(500, "An error occurred while retrieving investor transactions.");
        }
    }
}
