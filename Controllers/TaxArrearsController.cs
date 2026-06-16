using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TaxArrearsController : ControllerBase
    {
        private readonly ITaxArrearsService _service;
        private readonly ILogger<TaxArrearsController> _logger;

        public TaxArrearsController(ITaxArrearsService service, ILogger<TaxArrearsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET: api/TaxArrears?loanAliasIds=1&statuses=2
        [HttpGet]
        public async Task<ActionResult<List<TaxArrearsRowDto>>> Get(
            [FromQuery] int[]? loanAliasIds,
            [FromQuery] string[]? statuses,
            CancellationToken cancellationToken)
        {
            if (loanAliasIds is null || loanAliasIds.Length == 0 || loanAliasIds.Any(id => id <= 0))
            {
                return BadRequest("At least one valid loanAliasIds value is required.");
            }

            try
            {
                var result = await _service.GetAsync(loanAliasIds, statuses, cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get tax arrears rows cancelled");
                return StatusCode(499);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Get tax arrears rows validation failed");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tax arrears rows");
                return StatusCode(500, "An error occurred while retrieving tax arrears rows.");
            }
        }

        // GET: api/TaxArrears/lookups
        [HttpGet("lookups")]
        public ActionResult<TaxArrearsLookupsDto> GetLookups() =>
            Ok(_service.GetLookups());

        // POST: api/TaxArrears
        [HttpPost]
        public async Task<ActionResult<TaxArrearsRowDto>> Create(
            [FromBody] TaxArrearsCreateRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            try
            {
                var created = await _service.CreateAsync(request, cancellationToken);
                return StatusCode(StatusCodes.Status201Created, created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Create tax arrears row validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Create tax arrears row cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tax arrears row");
                return StatusCode(500, "An error occurred while creating the tax arrears record.");
            }
        }

        // PUT: api/TaxArrears
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] TaxArrearsBulkUpdateRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null || request.TaxArrears.Count == 0)
            {
                return BadRequest("Request body must include at least one tax arrears row.");
            }

            try
            {
                var updated = await _service.UpdateAsync(request, cancellationToken);
                return updated ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Update tax arrears rows validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update tax arrears rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tax arrears rows");
                return StatusCode(500, "An error occurred while updating tax arrears rows.");
            }
        }
    }
}
