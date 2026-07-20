using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NonKsServicedLoansController : ControllerBase
    {
        private readonly INonKsServicedLoansService _service;
        private readonly ILogger<NonKsServicedLoansController> _logger;

        public NonKsServicedLoansController(
            INonKsServicedLoansService service,
            ILogger<NonKsServicedLoansController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET: api/NonKsServicedLoans/lookups
        [HttpGet("lookups")]
        public async Task<ActionResult<NonKsServicedLoanLookupsDto>> GetLookups(CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _service.GetLookupsAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get non-KS serviced loan lookups cancelled");
                return StatusCode(499);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Get non-KS serviced loan lookups validation failed");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving non-KS serviced loan lookups");
                return StatusCode(500, "An error occurred while retrieving non-KS serviced loan lookups.");
            }
        }

        // GET: api/NonKsServicedLoans
        [HttpGet]
        public async Task<ActionResult<List<NonKsServicedLoanRowDto>>> GetAll(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.GetAllAsync(cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get non-KS serviced loans cancelled");
                return StatusCode(499);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Get non-KS serviced loans validation failed");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving non-KS serviced loans");
                return StatusCode(500, "An error occurred while retrieving non-KS serviced loans.");
            }
        }

        // POST: api/NonKsServicedLoans
        [HttpPost]
        public async Task<ActionResult<List<NonKsServicedLoanRowDto>>> Create(
            [FromBody] NonKsServicedLoanBulkCreateRequest? request,
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
                _logger.LogWarning(ex, "Create non-KS serviced loans validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Create non-KS serviced loans cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating non-KS serviced loans");
                return StatusCode(500, "An error occurred while creating non-KS serviced loans.");
            }
        }

        // PUT: api/NonKsServicedLoans
        [HttpPut]
        public async Task<ActionResult<List<NonKsServicedLoanRowDto>>> Update(
            [FromBody] NonKsServicedLoanBulkUpdateRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            try
            {
                var updated = await _service.UpdateAsync(request, cancellationToken);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Update non-KS serviced loans validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update non-KS serviced loans cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating non-KS serviced loans");
                return StatusCode(500, "An error occurred while updating non-KS serviced loans.");
            }
        }
    }
}
