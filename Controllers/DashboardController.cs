using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardService service, ILogger<DashboardController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET: api/dashboard?calendarYear=2024&widgets=portfolioValue,activeFunds,performanceChart
    /// <summary>
    /// Returns data for up to 5 selected widgets. <c>widgets</c> is required (comma-separated ids).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DashboardResponseDto>> Get(
        [FromQuery] int? calendarYear,
        [FromQuery] string widgets,
        CancellationToken cancellationToken)
    {
        if (!DashboardWidgetIds.TryParseWidgetQuery(widgets, out var widgetIds, out var validationError))
        {
            return BadRequest(validationError);
        }

        var year = calendarYear is > 1900 ? calendarYear.Value : DateTime.UtcNow.Year;

        try
        {
            var result = await _service.GetDashboardAsync(year, widgetIds, cancellationToken);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get dashboard cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error loading dashboard for {CalendarYear}", year);
            return StatusCode(500, "An error occurred while loading the dashboard.");
        }
    }

    // GET: api/dashboard/widgets
    /// <summary>Widget catalog for the Manage Widgets picker (id + display label).</summary>
    [HttpGet("widgets")]
    public ActionResult<IReadOnlyList<DashboardWidgetOptionDto>> GetWidgetCatalog() =>
        Ok(DashboardWidgetIds.GetCatalog());
}
