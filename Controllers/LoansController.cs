using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoansController : ControllerBase
    {
        private readonly ILoanService _service;
        private readonly ILogger<LoansController> _logger;

        public LoansController(
            ILoanService service,
            ILogger<LoansController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET: api/Loans
        [HttpGet]
        public async Task<ActionResult<List<LoanDto>>> GetAll()
        {
            try
            {
                var result = await _service.GetAllAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get all loan rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving loan rows");
                return StatusCode(500, "An error occurred while retrieving loan rows.");
            }
        }

        // PUT: api/Loans
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] LoanUpdateBatchRequest request)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            foreach (var loan in request.Loans)
            {
                if (!loan.LoanAliasKey.HasValue)
                {
                    return BadRequest("Loan alias key is required.");
                }

                if (string.IsNullOrWhiteSpace(loan.UserUpdatedBy))
                {
                    return BadRequest("User updated by is required.");
                }
            }

            try
            {
                var updated = await _service.UpdateAsync(request);
                return updated ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update loan rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating loan rows");
                return StatusCode(500, "An error occurred while updating loan rows.");
            }
        }
    }
}
