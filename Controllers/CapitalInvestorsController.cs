using kingsightapi.Entities;
using kingsightapi.Services;
using kingsightapi.Configuration;
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
    private readonly IPortalFilterService _filterService;
    private readonly ILogger<CapitalInvestorsController> _logger;

    public CapitalInvestorsController(
        IInvestorPortalService service,
        IPortalFilterService filterService,
        ILogger<CapitalInvestorsController> logger)
    {
        _service = service;
        _filterService = filterService;
        _logger = logger;
    }

    // GET: api/CapitalInvestors/filter-options
    [HttpGet("filter-options")]
    public async Task<ActionResult<InvestorListFilterOptionsDto>> GetFilterOptions()
    {
        try
        {
            return Ok(await _filterService.GetInvestorListFilterOptionsAsync());
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investor filter options cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving investor filter options");
            return StatusCode(500, "An error occurred while retrieving investor filter options.");
        }
    }

    // GET: api/CapitalInvestors?search=&view=ltd|quarterly&dateKey=&investorType=&relationship=&sortBy=&sortDir=asc|desc&page=1&pageSize=50
    [HttpGet]
    public async Task<ActionResult<PortalListPageResult<InvestorListItemDto, InvestorListSummaryDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] string? investorType,
        [FromQuery] string? relationship,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var resolvedView = view ?? TimeGranularity.Ltd;
        if (resolvedView == TimeGranularity.Quarterly && dateKey is null)
        {
            return BadRequest(
                $"Query parameter 'dateKey' is required when view is quarterly (yyyyMMdd from period dropdown).");
        }

        try
        {
            var period = dateKey is > 0 ? new FundPeriodFilter { DateKey = dateKey } : null;
            var result = await _service.GetInvestorsAsync(
                search, resolvedView, period, investorType, relationship, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investors cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving investors");
            return StatusCode(500, "An error occurred while retrieving investors.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/periods?view=ltd|quarterly|daily&source=commitments|nav|unfunded-commitments|investments|distributions&page=1&pageSize=50
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} periods for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving investor periods.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}?view=ltd|quarterly|daily&dateKey=
    [HttpGet("{investorKey:long}")]
    public async Task<ActionResult<InvestorProfileDto>> GetByKey(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey)
    {
        var resolvedView = view ?? TimeGranularity.Ltd;
        if (resolvedView == TimeGranularity.Quarterly && dateKey is null)
        {
            return BadRequest(
                $"Query parameter 'dateKey' is required when view is quarterly (yyyyMMdd from period dropdown).");
        }

        try
        {
            var period = dateKey is > 0 ? new FundPeriodFilter { DateKey = dateKey } : null;
            var result = await _service.GetInvestorByKeyAsync(investorKey, resolvedView, period);
            return result is null ? NotFound() : Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investor {InvestorKey} cancelled", investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving the investor.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/funds?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [HttpGet("{investorKey:long}/funds")]
    public async Task<ActionResult<PagedResult<InvestorInvestmentDto>>> GetFunds(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var resolvedView = view ?? TimeGranularity.Ltd;
        if (resolvedView == TimeGranularity.Quarterly && dateKey is null)
        {
            return BadRequest(
                $"Query parameter 'dateKey' is required when view is quarterly (yyyyMMdd from period dropdown).");
        }

        try
        {
            var period = dateKey is > 0 ? new FundPeriodFilter { DateKey = dateKey } : null;
            var result = await _service.GetInvestorFundsAsync(investorKey, resolvedView, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get funds for investor {InvestorKey} cancelled", investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving funds for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving investor funds.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/commitments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} commitments for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving investor commitments.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/unfunded-commitments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} unfunded commitments for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving unfunded commitments.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/investments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [HttpGet("{investorKey:long}/investments")]
    public async Task<ActionResult<PagedResult<FundGranularRowDto>>> GetInvestments(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var resolvedView = view ?? TimeGranularity.Ltd;
        if (resolvedView == TimeGranularity.Quarterly && dateKey is null)
        {
            return BadRequest(
                $"Query parameter 'dateKey' is required when view is quarterly (yyyyMMdd from period dropdown).");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey);
            var result = await _service.GetInvestorInvestmentActivityAsync(investorKey, resolvedView, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} investments for investor {InvestorKey} cancelled", resolvedView, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} investments for investor {InvestorKey}", resolvedView, investorKey);
            return StatusCode(500, "An error occurred while retrieving investor investments.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/fund-holdings
    [HttpGet("{investorKey:long}/fund-holdings")]
    public async Task<ActionResult<InvestorFundHoldingsResultDto>> GetFundHoldings(long investorKey)
    {
        try
        {
            var result = await _service.GetInvestorFundHoldingsAsync(investorKey);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get fund holdings for investor {InvestorKey} cancelled", investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving fund holdings for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving fund holdings.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/underlying-assets?page=1&pageSize=25&search=
    [HttpGet("{investorKey:long}/underlying-assets")]
    public async Task<ActionResult<PagedResult<InvestorUnderlyingAssetGridItemDto>>> GetUnderlyingAssets(
        long investorKey,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        try
        {
            var result = await _service.GetInvestorUnderlyingAssetsAsync(investorKey, search, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get underlying assets for investor {InvestorKey} cancelled", investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving underlying assets for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving underlying assets.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/distributions?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} distributions for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving investor distributions.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/nav?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} NAV for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving investor NAV.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/capital-activities?view=ltd|quarterly|daily&dateKey=&calendarYear=&search=&fundCode=&sortBy=&sortDir=&page=1&pageSize=50
    // Quarterly: pass dateKey for one quarter, or calendarYear without dateKey for all quarters in that year.
    [HttpGet("{investorKey:long}/capital-activities")]
    public async Task<ActionResult<PagedResult<InvestorFundCapitalActivitiesDto>>> GetCapitalActivities(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear,
        [FromQuery] string? search,
        [FromQuery] string? fundCode,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        if (view == TimeGranularity.Quarterly && dateKey is not > 0 && calendarYear is not > 1900)
        {
            return BadRequest("Pass dateKey for one quarter or calendarYear for all quarters in a year.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey, calendarYear);
            var result = await _service.GetInvestorCapitalActivitiesAsync(
                investorKey, view.Value, period, search, fundCode, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} capital activities for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} capital activities for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving capital activities.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/capital-activities/filters?view=ltd|quarterly|daily&dateKey=&calendarYear=
    [HttpGet("{investorKey:long}/capital-activities/filters")]
    public async Task<ActionResult<TransactionFilterOptionsDto>> GetCapitalActivitiesFilters(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        if (view == TimeGranularity.Quarterly && dateKey is not > 0 && calendarYear is not > 1900)
        {
            return BadRequest("Pass dateKey for one quarter or calendarYear for all quarters in a year.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey, calendarYear);
            return Ok(await _service.GetInvestorCapitalActivitiesFiltersAsync(investorKey, view.Value, period));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving capital activities filters for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving capital activities filters.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/distributions-table?view=ltd|quarterly|daily&dateKey=&calendarYear=&search=&fundCode=&sortBy=&sortDir=&page=1&pageSize=50
    [HttpGet("{investorKey:long}/distributions-table")]
    public async Task<ActionResult<PagedResult<InvestorFundDistributionsDto>>> GetDistributions(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear,
        [FromQuery] string? search,
        [FromQuery] string? fundCode,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        if (view == TimeGranularity.Quarterly && dateKey is not > 0 && calendarYear is not > 1900)
        {
            return BadRequest("Pass dateKey for one quarter or calendarYear for all quarters in a year.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey, calendarYear);
            var result = await _service.GetInvestorDistributionsSummaryAsync(
                investorKey, view.Value, period, search, fundCode, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} distributions for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} distributions for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving distributions.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/distributions-table/filters?view=ltd|quarterly|daily&dateKey=&calendarYear=
    [HttpGet("{investorKey:long}/distributions-table/filters")]
    public async Task<ActionResult<TransactionFilterOptionsDto>> GetDistributionsTableFilters(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        if (view == TimeGranularity.Quarterly && dateKey is not > 0 && calendarYear is not > 1900)
        {
            return BadRequest("Pass dateKey for one quarter or calendarYear for all quarters in a year.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey, calendarYear);
            return Ok(await _service.GetInvestorDistributionsFiltersAsync(investorKey, view.Value, period));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving distributions filters for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving distributions filters.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/irr?view=ltd|quarterly|daily&dateKey=&calendarYear=&search=&fundCode=&sortBy=&sortDir=&page=1&pageSize=50
    [HttpGet("{investorKey:long}/irr")]
    public async Task<ActionResult<PagedResult<InvestorFundIrrDto>>> GetIrr(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear,
        [FromQuery] string? search,
        [FromQuery] string? fundCode,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        if (view == TimeGranularity.Quarterly && dateKey is not > 0 && calendarYear is not > 1900)
        {
            return BadRequest("Pass dateKey for one quarter or calendarYear for all quarters in a year.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey, calendarYear);
            var result = await _service.GetInvestorIrrAsync(
                investorKey, view.Value, period, search, fundCode, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} IRR for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} IRR for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving IRR.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/irr/filters?view=ltd|quarterly|daily&dateKey=&calendarYear=
    [HttpGet("{investorKey:long}/irr/filters")]
    public async Task<ActionResult<TransactionFilterOptionsDto>> GetIrrFilters(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: {TimeGranularities.QueryValues}.");
        }

        if (view == TimeGranularity.Quarterly && dateKey is not > 0 && calendarYear is not > 1900)
        {
            return BadRequest("Pass dateKey for one quarter or calendarYear for all quarters in a year.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey, calendarYear);
            return Ok(await _service.GetInvestorIrrFiltersAsync(investorKey, view.Value, period));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving IRR filters for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving IRR filters.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/capital-obligations?view=quarterly&dateKey=&calendarYear=&search=&fundCode=&sortBy=&sortDir=&page=1&pageSize=50
    [HttpGet("{investorKey:long}/capital-obligations")]
    public async Task<ActionResult<PagedResult<InvestorFundObligationDto>>> GetCapitalObligations(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear,
        [FromQuery] string? search,
        [FromQuery] string? fundCode,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: quarterly.");
        }

        if (view != TimeGranularity.Quarterly)
        {
            return BadRequest("Capital obligations are only available when view is quarterly.");
        }

        if (dateKey is not > 0 && calendarYear is not > 1900)
        {
            return BadRequest("Pass dateKey for one quarter or calendarYear for all quarters in a year.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey, calendarYear);
            var result = await _service.GetInvestorCapitalObligationsAsync(
                investorKey, view.Value, period, search, fundCode, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} capital obligations for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} capital obligations for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving capital obligations.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/capital-obligations/filters?view=quarterly&dateKey=&calendarYear=
    [HttpGet("{investorKey:long}/capital-obligations/filters")]
    public async Task<ActionResult<TransactionFilterOptionsDto>> GetCapitalObligationsFilters(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: quarterly.");
        }

        if (view != TimeGranularity.Quarterly)
        {
            return BadRequest("Capital obligations filters are only available when view is quarterly.");
        }

        if (dateKey is not > 0 && calendarYear is not > 1900)
        {
            return BadRequest("Pass dateKey for one quarter or calendarYear for all quarters in a year.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey, calendarYear);
            return Ok(await _service.GetInvestorObligationsFiltersAsync(investorKey, view.Value, period));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving capital obligations filters for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving capital obligations filters.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/net-assets?view=quarterly&dateKey=&calendarYear=&search=&fundCode=&sortBy=&sortDir=&page=1&pageSize=50
    [HttpGet("{investorKey:long}/net-assets")]
    public async Task<ActionResult<PagedResult<InvestorFundNetAssetsDto>>> GetNetAssets(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear,
        [FromQuery] string? search,
        [FromQuery] string? fundCode,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: quarterly.");
        }

        if (view != TimeGranularity.Quarterly)
        {
            return BadRequest("Net assets are only available when view is quarterly.");
        }

        if (dateKey is not > 0 && calendarYear is not > 1900)
        {
            return BadRequest("Pass dateKey for one quarter or calendarYear for all quarters in a year.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey, calendarYear);
            var result = await _service.GetInvestorNetAssetsAsync(
                investorKey, view.Value, period, search, fundCode, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get net assets for investor {InvestorKey} cancelled", investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving net assets for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving net assets.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/net-assets/filters?view=quarterly&dateKey=&calendarYear=
    [HttpGet("{investorKey:long}/net-assets/filters")]
    public async Task<ActionResult<TransactionFilterOptionsDto>> GetNetAssetsFilters(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear)
    {
        if (view is null)
        {
            return BadRequest(
                $"Query parameter '{TimeGranularities.QueryParameterName}' is required. Valid values: quarterly.");
        }

        if (view != TimeGranularity.Quarterly)
        {
            return BadRequest("Net assets filters are only available when view is quarterly.");
        }

        if (dateKey is not > 0 && calendarYear is not > 1900)
        {
            return BadRequest("Pass dateKey for one quarter or calendarYear for all quarters in a year.");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey, calendarYear);
            return Ok(await _service.GetInvestorNetAssetsFiltersAsync(investorKey, view.Value, period));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving net assets filters for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving net assets filters.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/fund-exposure?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [HttpGet("{investorKey:long}/fund-exposure")]
    public async Task<ActionResult<PagedResult<InvestorFundExposureDto>>> GetFundExposure(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
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
            var period = BuildPeriodFilter(dateKey);
            var result = await _service.GetInvestorFundExposureAsync(investorKey, view.Value, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} fund exposure for investor {InvestorKey} cancelled", view, investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} fund exposure for investor {InvestorKey}", view, investorKey);
            return StatusCode(500, "An error occurred while retrieving fund exposure.");
        }
    }

    // GET: api/CapitalInvestors/{investorKey}/assets?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [HttpGet("{investorKey:long}/assets")]
    public async Task<ActionResult<PagedResult<InvestorUnderlyingAssetDto>>> GetAssets(
        long investorKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var resolvedView = view ?? TimeGranularity.Ltd;
        if (resolvedView == TimeGranularity.Quarterly && dateKey is null)
        {
            return BadRequest(
                $"Query parameter 'dateKey' is required when view is quarterly (yyyyMMdd from period dropdown).");
        }

        try
        {
            var period = BuildPeriodFilter(dateKey);
            var result = await _service.GetInvestorAssetsAsync(investorKey, resolvedView, period, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get assets for investor {InvestorKey} cancelled", investorKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving assets for investor {InvestorKey}", investorKey);
            return StatusCode(500, "An error occurred while retrieving investor assets.");
        }
    }

    private static FundPeriodFilter? BuildPeriodFilter(int? dateKey, int? calendarYear = null)
    {
        if (dateKey is not > 0 && calendarYear is not > 1900)
        {
            return null;
        }

        return new FundPeriodFilter
        {
            DateKey = dateKey is > 0 ? dateKey : null,
            CalendarYear = calendarYear is > 1900 ? calendarYear : null
        };
    }
}
