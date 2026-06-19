using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ManagementSummaryController : ControllerBase
    {
        private readonly IManagementSummaryService _service;
        private readonly ILogger<ManagementSummaryController> _logger;

        public ManagementSummaryController(
            IManagementSummaryService service,
            ILogger<ManagementSummaryController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET: api/ManagementSummary/dashboard?asOfDate=2025-08-31&statuses=Default&riskLevels=HIGH
        [HttpGet("dashboard")]
        public async Task<ActionResult<ManagementSummaryDashboardDto>> GetDashboard(
            [FromQuery] DateOnly asOfDate,
            [FromQuery] DateOnly? defaultDateFrom,
            [FromQuery] DateOnly? defaultDateTo,
            [FromQuery] DateOnly? maturityDateFrom,
            [FromQuery] DateOnly? maturityDateTo,
            [FromQuery] string? sponsor,
            [FromQuery] string[]? riskLevels,
            [FromQuery] string[]? statuses,
            [FromQuery] string[]? investorAliases,
            [FromQuery] int[]? loanAliasIds,
            CancellationToken cancellationToken)
        {
            if (asOfDate == default)
            {
                return BadRequest("asOfDate is required (ISO date, e.g. 2025-08-31).");
            }

            if (loanAliasIds is not null && loanAliasIds.Any(id => id <= 0))
            {
                return BadRequest("loanAliasIds must contain positive integers.");
            }

            var query = new ManagementSummaryDashboardQuery
            {
                AsOfDate = asOfDate,
                DefaultDateFrom = defaultDateFrom,
                DefaultDateTo = defaultDateTo,
                MaturityDateFrom = maturityDateFrom,
                MaturityDateTo = maturityDateTo,
                Sponsor = sponsor,
                RiskLevels = riskLevels,
                Statuses = statuses,
                InvestorAliases = investorAliases,
                LoanAliasIds = loanAliasIds
            };

            try
            {
                var result = await _service.GetDashboardAsync(query, cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get management summary dashboard cancelled");
                return StatusCode(499);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Get management summary dashboard validation failed");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving management summary dashboard");
                return StatusCode(500, "An error occurred while retrieving management summary dashboard.");
            }
        }

        // GET: api/ManagementSummary/{loanAliasKey}/loan-detail-report?asOfDate=2025-08-31
        [HttpGet("{loanAliasKey:int}/loan-detail-report")]
        public async Task<ActionResult<LoanDetailReportDashboardDto>> GetLoanDetailReport(
            int loanAliasKey,
            [FromQuery] DateOnly asOfDate,
            [FromQuery] string[]? statuses,
            CancellationToken cancellationToken)
        {
            if (loanAliasKey <= 0)
            {
                return BadRequest("loanAliasKey must be a positive integer.");
            }

            if (asOfDate == default)
            {
                return BadRequest("asOfDate is required (ISO date, e.g. 2025-08-31).");
            }

            var query = new LoanDetailReportQuery
            {
                AsOfDate = asOfDate,
                Statuses = statuses
            };

            try
            {
                var result = await _service.GetLoanDetailReportAsync(loanAliasKey, query, cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(
                    "Get loan detail report dashboard cancelled for alias {LoanAliasKey}",
                    loanAliasKey);
                return StatusCode(499);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Get loan detail report dashboard validation failed for alias {LoanAliasKey}",
                    loanAliasKey);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving loan detail report dashboard for alias {LoanAliasKey}",
                    loanAliasKey);
                return StatusCode(500, "An error occurred while retrieving loan detail report.");
            }
        }

        // Legacy list endpoint — prefer GET /api/ManagementSummary/dashboard for SPA.
        // GET: api/ManagementSummary?loanAliasIds=1&statuses=2
        [HttpGet]
        public async Task<ActionResult<List<ManagementSummaryRowDto>>> GetSummary(
            [FromQuery] int[]? loanAliasIds,
            [FromQuery] string[]? statuses,
            CancellationToken cancellationToken)
        {
            if (loanAliasIds is not null && loanAliasIds.Any(id => id <= 0))
            {
                return BadRequest("loanAliasIds must contain positive integers.");
            }

            try
            {
                var result = await _service.GetSummaryAsync(loanAliasIds, statuses, cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get management summary cancelled");
                return StatusCode(499);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Get management summary validation failed");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving management summary");
                return StatusCode(500, "An error occurred while retrieving management summary.");
            }
        }

        // Legacy detail list — prefer GET /api/ManagementSummary/{loanAliasKey}/loan-detail-report.
        // GET: api/ManagementSummary/{loanAliasKey}/loan-details?statuses=2
        [HttpGet("{loanAliasKey:int}/loan-details")]
        public async Task<ActionResult<List<LoanDetailReportRowDto>>> GetLoanDetails(
            int loanAliasKey,
            [FromQuery] string[]? statuses,
            CancellationToken cancellationToken)
        {
            if (loanAliasKey <= 0)
            {
                return BadRequest("loanAliasKey must be a positive integer.");
            }

            try
            {
                var result = await _service.GetLoanDetailsAsync(loanAliasKey, statuses, cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get loan detail report cancelled for alias {LoanAliasKey}", loanAliasKey);
                return StatusCode(499);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Get loan detail report validation failed for alias {LoanAliasKey}", loanAliasKey);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving loan detail report for alias {LoanAliasKey}", loanAliasKey);
                return StatusCode(500, "An error occurred while retrieving loan detail report.");
            }
        }
    }
}
