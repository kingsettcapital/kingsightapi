using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

/// <summary>
/// Kingsight capital portal — investor list and detail (mirrors <see cref="FundsController"/> scoped by investor).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CapitalInvestorsController : ControllerBase
{
    private readonly IInvestorPortalService _service;
    private readonly ILogger<CapitalInvestorsController> _logger;

    public CapitalInvestorsController(IInvestorPortalService service, ILogger<CapitalInvestorsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET: api/CapitalInvestors?search=&page=1&pageSize=50
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

    // GET: api/CapitalInvestors/{investorKey}/periods?view=ltd|quarterly|daily&source=commitments|nav|unfunded-commitments|investments|distributions&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{investorKey:long}/periods")]
    public async Task<ActionResult<PagedResult<FundPeriodDto>>> GetPeriods(
        long investorKey,
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
            var result = await _service.GetInvestorPeriodsAsync(investorKey, view.Value, metricSource, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} periods for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} periods for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving investor periods.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}
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

    // GET: api/CapitalInvestors/{investorKey}/funds?page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{investorKey:long}/funds")]
    public async Task<ActionResult<PagedResult<InvestorInvestmentDto>>> GetFunds(
        long investorKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _service.GetInvestorFundsAsync(investorKey, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get funds for investor {InvestorKey} cancelled", investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving funds for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving investor funds.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/commitments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{investorKey:long}/commitments")]
    public async Task<ActionResult<PagedResult<FundGranularRowDto>>> GetCommitments(
        long investorKey,
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
            var result = await _service.GetInvestorCommitmentsAsync(investorKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} commitments for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} commitments for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving investor commitments.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/unfunded-commitments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{investorKey:long}/unfunded-commitments")]
    public async Task<ActionResult<PagedResult<FundGranularRowDto>>> GetUnfundedCommitments(
        long investorKey,
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
            var result = await _service.GetInvestorUnfundedCommitmentsAsync(investorKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} unfunded commitments for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} unfunded commitments for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving unfunded commitments.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/investments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{investorKey:long}/investments")]
    public async Task<ActionResult<PagedResult<FundGranularRowDto>>> GetInvestments(
        long investorKey,
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
            var result = await _service.GetInvestorInvestmentActivityAsync(investorKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} investments for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} investments for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving investor investments.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/distributions?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{investorKey:long}/distributions")]
    public async Task<ActionResult<PagedResult<FundDistributionGroupDto>>> GetDistributions(
        long investorKey,
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
            var result = await _service.GetInvestorDistributionsAsync(investorKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} distributions for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} distributions for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving investor distributions.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/nav?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [AllowAnonymous]
    [HttpGet("{investorKey:long}/nav")]
    public async Task<ActionResult<PagedResult<FundGranularRowDto>>> GetNav(
        long investorKey,
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
            var result = await _service.GetInvestorNavAsync(investorKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} NAV for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} NAV for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving investor NAV.");
        }
    }

    private static FundPeriodFilter? BuildPeriodFilter(int? dateKey) =>
        dateKey is > 0 ? new FundPeriodFilter { DateKey = dateKey } : null;
}
