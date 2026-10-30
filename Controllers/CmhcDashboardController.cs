using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

/// <summary>
/// CMHC Monday pipeline dashboard — live pipeline grid and shared report state.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CmhcDashboardController : ControllerBase
{
    private readonly ICmhcDashboardService _service;
    private readonly ICurrentUserResolver _currentUserResolver;
    private readonly ILogger<CmhcDashboardController> _logger;

    public CmhcDashboardController(
        ICmhcDashboardService service,
        ICurrentUserResolver currentUserResolver,
        ILogger<CmhcDashboardController> logger)
    {
        _service = service;
        _currentUserResolver = currentUserResolver;
        _logger = logger;
    }

    // GET: api/CmhcDashboard/pipeline?asOf=yyyy-MM-dd
    [HttpGet("pipeline")]
    public async Task<ActionResult<CmhcPipelineDto>> GetPipeline(
        [FromQuery] string? asOf,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetPipelineAsync(asOf, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get CMHC pipeline cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving CMHC pipeline");
            return StatusCode(500, "An error occurred while retrieving the CMHC pipeline.");
        }
    }

    // GET: api/CmhcDashboard/state/{key}
    [HttpGet("state/{key}")]
    public async Task<ActionResult<CmhcReportStateDto>> GetState(string key, CancellationToken cancellationToken)
    {
        if (!CmhcDashboardService.AllowedStateKeys.Contains(key))
        {
            return BadRequest($"Unknown state key '{key}'.");
        }

        try
        {
            return Ok(await _service.GetStateAsync(key, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get CMHC state cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving CMHC report state");
            return StatusCode(500, "An error occurred while retrieving the CMHC report state.");
        }
    }

    // PUT: api/CmhcDashboard/state/{key}  body: { value, version }  -> 409 when version is stale
    [HttpPut("state/{key}")]
    public async Task<ActionResult<CmhcReportStateDto>> PutState(
        string key,
        [FromBody] CmhcReportStateWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!CmhcDashboardService.AllowedStateKeys.Contains(key))
        {
            return BadRequest($"Unknown state key '{key}'.");
        }

        try
        {
            var (displayName, error) = await _currentUserResolver.RequireAuditDisplayNameAsync(
                null, "updatedBy", cancellationToken);
            if (error is not null)
            {
                return error;
            }

            var result = await _service.SaveStateAsync(
                key, request.Value, request.Version, displayName, cancellationToken);

            return result.Status == CmhcReportStateWriteStatus.Conflict
                ? Conflict(result.State)
                : Ok(result.State);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Save CMHC state cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error saving CMHC report state");
            return StatusCode(500, "An error occurred while saving the CMHC report state.");
        }
    }
}
