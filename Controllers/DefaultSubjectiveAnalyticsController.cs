using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DefaultSubjectiveAnalyticsController : ControllerBase
    {
        private readonly IDefaultSubjectiveAnalyticsService _service;
        private readonly ILoanSecurityValueService _loanSecurityValueService;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly ILogger<DefaultSubjectiveAnalyticsController> _logger;

        public DefaultSubjectiveAnalyticsController(
            IDefaultSubjectiveAnalyticsService service,
            ILoanSecurityValueService loanSecurityValueService,
            ICurrentUserResolver currentUserResolver,
            ILogger<DefaultSubjectiveAnalyticsController> logger)
        {
            _service = service;
            _loanSecurityValueService = loanSecurityValueService;
            _currentUserResolver = currentUserResolver;
            _logger = logger;
        }

        // GET: api/DefaultSubjectiveAnalytics?loanAliasIds=1&statuses=2
        [HttpGet]
        public async Task<ActionResult<List<DefaultSubjectiveAnalyticsRowDto>>> Get(
            [FromQuery] int[]? loanAliasIds,
            [FromQuery] string[]? statuses,
            CancellationToken cancellationToken)
        {
            try
            {
                var aliasFilter = loanAliasIds?.Where(id => id > 0).ToArray();
                var result = await _service.GetAsync(
                    aliasFilter is { Length: > 0 } ? aliasFilter : null,
                    statuses,
                    cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get default subjective analytics rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default subjective analytics rows");
                return StatusCode(500, "An error occurred while retrieving default subjective analytics rows.");
            }
        }

        // GET: api/DefaultSubjectiveAnalytics/statuses
        [HttpGet("statuses")]
        public async Task<ActionResult<List<LoanSecurityValueStatusOptionDto>>> GetStatuses(
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _loanSecurityValueService.GetStatusOptionsAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get default subjective analytics status options cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default subjective analytics status options");
                return StatusCode(500, "An error occurred while retrieving status filter options.");
            }
        }

        // GET: api/DefaultSubjectiveAnalytics/lookups (SPA: dropdown options)
        [HttpGet("lookups")]
        public ActionResult<DefaultSubjectiveAnalyticsLookupsDto> GetLookups() =>
            Ok(_service.GetLookups());

        // GET: api/DefaultSubjectiveAnalytics/default-status-options
        [HttpGet("default-status-options")]
        public ActionResult<IReadOnlyList<DefaultSubjectiveAnalyticsOptionDto>> GetDefaultStatusOptions() =>
            Ok(_service.GetDefaultStatusOptions());

        // GET: api/DefaultSubjectiveAnalytics/exit-plan-options
        [HttpGet("exit-plan-options")]
        public ActionResult<IReadOnlyList<DefaultSubjectiveAnalyticsOptionDto>> GetExitPlanOptions() =>
            Ok(_service.GetExitPlanOptions());

        // PUT: api/DefaultSubjectiveAnalytics
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] DefaultSubjectiveAnalyticsBulkUpdateRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null || request.Loans.Count == 0)
            {
                return BadRequest("Request body must include at least one loan row.");
            }

            foreach (var loan in request.Loans)
            {
                var validationError = DefaultSubjectiveAnalyticsValidation.ValidateUpdateItem(loan);
                if (validationError is not null)
                {
                    return BadRequest(validationError);
                }
            }

            var clientAudit = request.Loans.FirstOrDefault()?.UserUpdatedBy;
            var (auditDisplayName, auditError) = await _currentUserResolver.RequireAuditDisplayNameAsync(
                clientAudit,
                "userUpdatedBy",
                cancellationToken);
            if (auditError is not null)
            {
                return auditError;
            }

            try
            {
                var updated = await _service.UpdateAsync(request, auditDisplayName!, cancellationToken);
                return updated ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Default subjective analytics update validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update default subjective analytics rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating default subjective analytics rows");
                return StatusCode(500, "An error occurred while updating default subjective analytics rows.");
            }
        }
    }
}
