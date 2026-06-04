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

    // GET: api/funds/{fundKey}/periods?view=ltd|quarterly|daily&source=commitments|nav|unfunded-commitments|investments|distributions&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{fundKey:int}/periods")]
    public async Task<ActionResult<PagedResult<FundPeriodDto>>> GetFundPeriods(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        if (!FundMetricSources.TryParse(source, out var metricSource))
        {
            return BadRequest($"Query parameter 'source' is required. Valid values: {FundMetricSources.QueryValues}.");
        }

        try
        {
            var result = await _service.GetFundPeriodsAsync(fundKey, view.Value, metricSource, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} periods for fund {FundKey} cancelled", view, fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} periods for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving fund periods.");
        }
    }

    // GET: api/funds/{fundKey}
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

    // GET: api/funds/{fundKey}/investors?search=
    [HttpGet("{fundKey:int}/investors")]
    public async Task<ActionResult<IReadOnlyList<FundInvestorDto>>> GetInvestors(
        int fundKey,
        [FromQuery] string? search)
    {
        try
        {
            var result = await _service.GetFundInvestorsAsync(fundKey, search);
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

    // GET: api/funds/{fundKey}/commitments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{fundKey:int}/commitments")]
    public async Task<ActionResult<PagedResult<FundGranularRowDto>>> GetCommitments(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey);
            var result = await _service.GetFundCommitmentsAsync(fundKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} commitments for fund {FundKey} cancelled", view, fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} commitments for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving fund commitments.");
        }
    }

    // GET: api/funds/{fundKey}/unfunded-commitments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{fundKey:int}/unfunded-commitments")]
    public async Task<ActionResult<PagedResult<FundGranularRowDto>>> GetUnfundedCommitments(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey);
            var result = await _service.GetFundUnfundedCommitmentsAsync(fundKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} unfunded commitments for fund {FundKey} cancelled", view, fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} unfunded commitments for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving unfunded commitments.");
        }
    }

    // GET: api/funds/{fundKey}/investments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{fundKey:int}/investments")]
    public async Task<ActionResult<PagedResult<FundGranularRowDto>>> GetInvestments(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey);
            var result = await _service.GetFundInvestmentsAsync(fundKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} investments for fund {FundKey} cancelled", view, fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} investments for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving fund investments.");
        }
    }

    // GET: api/funds/{fundKey}/distributions?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{fundKey:int}/distributions")]
    public async Task<ActionResult<PagedResult<FundGranularRowDto>>> GetDistributions(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey);
            var result = await _service.GetFundDistributionsAsync(fundKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} distributions for fund {FundKey} cancelled", view, fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} distributions for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving fund distributions.");
        }
    }

    // GET: api/funds/{fundKey}/nav?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [HttpGet("{fundKey:int}/nav")]
    public async Task<ActionResult<PagedResult<FundGranularRowDto>>> GetNav(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey);
            var result = await _service.GetFundNavAsync(fundKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} NAV for fund {FundKey} cancelled", view, fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} NAV for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving fund NAV.");
        }
    }

    private static FundPeriodFilter? BuildPeriodFilter(int? dateKey) =>
        dateKey is > 0 ? new FundPeriodFilter { DateKey = dateKey } : null;
}
