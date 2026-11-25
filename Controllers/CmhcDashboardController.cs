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
    private readonly ICmhcAccessService _accessService;
    private readonly ICurrentUserResolver _currentUserResolver;
    private readonly ILogger<CmhcDashboardController> _logger;

    private const string NoAccessMessage =
        "You don't have an CMHC sub-role. Ask an administrator to assign you one.";
    private const string SharedStateForbiddenMessage =
        "Only Admins and Reviewers can change shared CMHC dashboard settings.";

    /// <summary>Report state that carries loan-level data; scoped users get an empty copy (ACC-08).</summary>
    private static readonly IReadOnlySet<string> LoanStateKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "hist", "alloc-coverage", "alloc-active", "alloc-active-reset", "renewal-milestones", "sponsor-pairs",
        "wip-marks", "wip-credits", "ffa-stamps"
    };

    public CmhcDashboardController(
        ICmhcDashboardService service,
        ICmhcAccessService accessService,
        ICurrentUserResolver currentUserResolver,
        ILogger<CmhcDashboardController> logger)
    {
        _service = service;
        _accessService = accessService;
        _currentUserResolver = currentUserResolver;
        _logger = logger;
    }

    // GET: api/CmhcDashboard/access/me
    [HttpGet("access/me")]
    public async Task<ActionResult<CmhcMyAccessDto>> GetMyAccess(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _accessService.GetCurrentAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving CMHC dashboard access");
            return StatusCode(500, "An error occurred while retrieving your dashboard access.");
        }
    }

    // GET: api/CmhcDashboard/pipeline?asOf=yyyy-MM-dd
    [HttpGet("pipeline")]
    public async Task<ActionResult<CmhcPipelineDto>> GetPipeline(
        [FromQuery] string? asOf,
        CancellationToken cancellationToken)
    {
        try
        {
            var access = await _accessService.GetCurrentAsync(cancellationToken);
            if (!access.HasAccess)
            {
                return StatusCode(StatusCodes.Status403Forbidden, NoAccessMessage);
            }

            var pipeline = await _service.GetPipelineAsync(asOf, cancellationToken);
            return Ok(CmhcDashboardService.FilterForAccess(pipeline, access));
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
            var access = await _accessService.GetCurrentAsync(cancellationToken);
            if (!access.HasAccess)
            {
                return StatusCode(StatusCodes.Status403Forbidden, NoAccessMessage);
            }

            if (!access.SeesAllLoans && LoanStateKeys.Contains(key))
            {
                return Ok(new CmhcReportStateDto { Key = key, Value = null, Version = 0 });
            }

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
            var access = await _accessService.GetCurrentAsync(cancellationToken);
            if (!access.SeesAllLoans)
            {
                return StatusCode(StatusCodes.Status403Forbidden, SharedStateForbiddenMessage);
            }

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
