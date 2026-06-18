using kingsightapi.Entities;

using kingsightapi.Services;
using kingsightapi.Configuration;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;



namespace kingsightapi.Controllers;



[ApiController]
[Route("api/[controller]")]
public class FundsController : ControllerBase
{
    private readonly IFundPortalService _service;
    private readonly IPortalFilterService _filterService;
    private readonly ILogger<FundsController> _logger;

    public FundsController(
        IFundPortalService service,
        IPortalFilterService filterService,
        ILogger<FundsController> logger)
    {
        _service = service;
        _filterService = filterService;
        _logger = logger;
    }

    // GET: api/funds/filter-options
    [HttpGet("filter-options")]
    public async Task<ActionResult<FundListFilterOptionsDto>> GetFilterOptions()
    {
        try
        {
            return Ok(await _filterService.GetFundListFilterOptionsAsync());
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get fund filter options cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving fund filter options");
            return StatusCode(500, "An error occurred while retrieving fund filter options.");
        }
    }
    // GET: api/funds?search=&view=ltd|quarterly&dateKey=&fundType=&strategy=&sortBy=&sortDir=asc|desc&page=1&pageSize=50
    [HttpGet]
    public async Task<ActionResult<PortalListPageResult<FundListItemDto, FundListSummaryDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] string? fundType,
        [FromQuery] string? strategy,
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
            var result = await _service.GetFundsAsync(
                search, resolvedView, period, fundType, strategy, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get funds cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving funds");
            return StatusCode(500, "An error occurred while retrieving funds.");
        }
    }

    // GET: api/funds/{fundKey}/periods?view=ltd|quarterly|daily&source=commitments|nav|unfunded-commitments|investments|distributions&page=1&pageSize=50
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} periods for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving fund periods.");
        }
    }

    // GET: api/funds/{fundKey}
    [HttpGet("{fundKey:int}")]
    public async Task<ActionResult<FundProfileDto>> GetByKey(int fundKey)
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving the fund.");
        }
    }

    // GET: api/funds/{fundKey}/assets?page=1&pageSize=50
    [HttpGet("{fundKey:int}/assets")]
    public async Task<ActionResult<PagedResult<FundAssetDto>>> GetAssets(
        int fundKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _service.GetFundAssetsAsync(fundKey, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get assets for fund {FundKey} cancelled", fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving assets for fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving fund assets.");
        }
    }

    // GET: api/funds/{fundKey}/investors?search=&page=1&pageSize=50
    [HttpGet("{fundKey:int}/investors")]
    public async Task<ActionResult<PagedResult<FundInvestorDto>>> GetInvestors(
        int fundKey,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _service.GetFundInvestorsAsync(fundKey, search, page, pageSize);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investors for fund {FundKey} cancelled", fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving investors for fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving fund investors.");
        }
    }

    // GET: api/funds/{fundKey}/commitments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} commitments for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving fund commitments.");
        }
    }

    // GET: api/funds/{fundKey}/unfunded-commitments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} unfunded commitments for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving unfunded commitments.");
        }
    }

    // GET: api/funds/{fundKey}/investments?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} investments for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving fund investments.");
        }
    }

    // GET: api/funds/{fundKey}/distributions?view=ltd|quarterly|daily&dateKey=&page=1&pageSize=50
    [HttpGet("{fundKey:int}/distributions")]
    public async Task<ActionResult<PagedResult<FundDistributionGroupDto>>> GetDistributions(
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} distributions for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving fund distributions.");
        }
    }

    // GET: api/funds/{fundKey}/capital-activities?view=ltd|quarterly|daily&dateKey=&calendarYear=&search=&investorName=&sortBy=&sortDir=&page=1&pageSize=50
    [HttpGet("{fundKey:int}/capital-activities")]
    public async Task<ActionResult<PagedResult<FundInvestorCapitalActivitiesDto>>> GetCapitalActivities(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear,
        [FromQuery] string? search,
        [FromQuery] string? investorName,
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
            var result = await _service.GetFundCapitalActivitiesAsync(
                fundKey, view.Value, period, search, investorName, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} capital activities for fund {FundKey} cancelled", view, fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} capital activities for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving capital activities.");
        }
    }

    // GET: api/funds/{fundKey}/capital-activities/filters?view=ltd|quarterly|daily&dateKey=&calendarYear=
    [HttpGet("{fundKey:int}/capital-activities/filters")]
    public async Task<ActionResult<TransactionFilterOptionsDto>> GetCapitalActivitiesFilters(
        int fundKey,
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
            return Ok(await _service.GetFundCapitalActivitiesFiltersAsync(fundKey, view.Value, period));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving capital activities filters for fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving capital activities filters.");
        }
    }

    // GET: api/funds/{fundKey}/distributions-table?view=ltd|quarterly|daily&dateKey=&calendarYear=&search=&investorName=&sortBy=&sortDir=&page=1&pageSize=50
    [HttpGet("{fundKey:int}/distributions-table")]
    public async Task<ActionResult<PagedResult<FundInvestorDistributionsDto>>> GetDistributionsTable(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear,
        [FromQuery] string? search,
        [FromQuery] string? investorName,
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
            var result = await _service.GetFundDistributionsSummaryAsync(
                fundKey, view.Value, period, search, investorName, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} distributions table for fund {FundKey} cancelled", view, fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} distributions table for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving distributions.");
        }
    }

    // GET: api/funds/{fundKey}/distributions-table/filters?view=ltd|quarterly|daily&dateKey=&calendarYear=
    [HttpGet("{fundKey:int}/distributions-table/filters")]
    public async Task<ActionResult<TransactionFilterOptionsDto>> GetDistributionsTableFilters(
        int fundKey,
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
            return Ok(await _service.GetFundDistributionsFiltersAsync(fundKey, view.Value, period));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving distributions filters for fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving distributions filters.");
        }
    }

    // GET: api/funds/{fundKey}/irr?view=ltd|quarterly|daily&dateKey=&calendarYear=&search=&investorName=&sortBy=&sortDir=&page=1&pageSize=50
    [HttpGet("{fundKey:int}/irr")]
    public async Task<ActionResult<PagedResult<FundInvestorIrrDto>>> GetIrr(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear,
        [FromQuery] string? search,
        [FromQuery] string? investorName,
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
            var result = await _service.GetFundIrrAsync(
                fundKey, view.Value, period, search, investorName, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} IRR for fund {FundKey} cancelled", view, fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} IRR for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving IRR.");
        }
    }

    // GET: api/funds/{fundKey}/irr/filters?view=ltd|quarterly|daily&dateKey=&calendarYear=
    [HttpGet("{fundKey:int}/irr/filters")]
    public async Task<ActionResult<TransactionFilterOptionsDto>> GetIrrFilters(
        int fundKey,
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
            return Ok(await _service.GetFundIrrFiltersAsync(fundKey, view.Value, period));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving IRR filters for fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving IRR filters.");
        }
    }

    // GET: api/funds/{fundKey}/capital-obligations?view=quarterly&dateKey=&calendarYear=&search=&investorName=&sortBy=&sortDir=&page=1&pageSize=50
    [HttpGet("{fundKey:int}/capital-obligations")]
    public async Task<ActionResult<PagedResult<FundInvestorObligationDto>>> GetCapitalObligations(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear,
        [FromQuery] string? search,
        [FromQuery] string? investorName,
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
            var result = await _service.GetFundCapitalObligationsAsync(
                fundKey, view.Value, period, search, investorName, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} capital obligations for fund {FundKey} cancelled", view, fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} capital obligations for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving capital obligations.");
        }
    }

    // GET: api/funds/{fundKey}/capital-obligations/filters?view=quarterly&dateKey=&calendarYear=
    [HttpGet("{fundKey:int}/capital-obligations/filters")]
    public async Task<ActionResult<TransactionFilterOptionsDto>> GetCapitalObligationsFilters(
        int fundKey,
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
            return Ok(await _service.GetFundObligationsFiltersAsync(fundKey, view.Value, period));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving capital obligations filters for fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving capital obligations filters.");
        }
    }

    // GET: api/funds/{fundKey}/net-assets?view=quarterly&dateKey=&calendarYear=&search=&investorName=&sortBy=&sortDir=&page=1&pageSize=50
    [HttpGet("{fundKey:int}/net-assets")]
    public async Task<ActionResult<PagedResult<FundInvestorNetAssetsDto>>> GetNetAssets(
        int fundKey,
        [FromQuery] TimeGranularity? view,
        [FromQuery] int? dateKey,
        [FromQuery] int? calendarYear,
        [FromQuery] string? search,
        [FromQuery] string? investorName,
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
            var result = await _service.GetFundNetAssetsAsync(
                fundKey, view.Value, period, search, investorName, sortBy, sortDir, page, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get net assets for fund {FundKey} cancelled", fundKey);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving net assets for fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving net assets.");
        }
    }

    // GET: api/funds/{fundKey}/net-assets/filters?view=quarterly&dateKey=&calendarYear=
    [HttpGet("{fundKey:int}/net-assets/filters")]
    public async Task<ActionResult<TransactionFilterOptionsDto>> GetNetAssetsFilters(
        int fundKey,
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
            return Ok(await _service.GetFundNetAssetsFiltersAsync(fundKey, view.Value, period));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving net assets filters for fund {FundKey}", fundKey);
            return StatusCode(500, "An error occurred while retrieving net assets filters.");
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
            ConnectionLogging.LogControllerError(_logger, ex, "Error retrieving {View} NAV for fund {FundKey}", view, fundKey);
            return StatusCode(500, "An error occurred while retrieving fund NAV.");
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
