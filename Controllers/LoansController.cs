using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using kingsightapi.Services;
using kingsightapi.Entities;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoansController : ControllerBase
    {
        private readonly ILoanService _service;
        private readonly ILogger<LoansController> _logger;

        public LoansController(ILoanService service, ILogger<LoansController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET: api/funds
        [HttpGet]
        public async Task<ActionResult<List<FundDto>>> Get()
        {
            try
            {
                var result = await _service.GetLoansAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get all funds cancelled");
                return StatusCode(499); // Client Closed Request (non-standard) � indicates cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving funds");
                return StatusCode(500, "An error occurred while retrieving funds.");
            }
        }

        // GET: api/loans/investor-details
        [HttpGet("investor-details")]
        public async Task<ActionResult<List<InvestorDto>>> GetInvestorDetails()
        {
            try
            {
                var result = await _service.GetInvestorDetailsAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get investor details cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving investor details");
                return StatusCode(500, "An error occurred while retrieving investor details.");
            }
        }

        // PUT: api/loans/{loanKey}/investor-alias
        [HttpPut("{loanKey:int}/investor-alias")]
        public async Task<IActionResult> PutInvestorAlias(int loanKey, [FromBody] InvestorAliasUpdateRequest request)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            try
            {
                var updated = await _service.UpdateInvestorAliasAsync(loanKey, request);
                return updated ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update investor alias for loan {LoanKey} cancelled", loanKey);
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating investor alias for loan {LoanKey}", loanKey);
                return StatusCode(500, "An error occurred while updating the investor alias.");
            }
        }

        // GET: api/loans/loanalias
        [HttpGet("loanalias")]
        public async Task<ActionResult<List<LoanAlias>>> GetLoanAlias()
        {
            try
            {
                var result = await _service.GetLoanAlias();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get loan alias cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving loan alias");
                return StatusCode(500, "An error occurred while retrieving loan aliases.");
            }
        }

        // PUT: api/loans/loanalias
        [HttpPut("loanalias")]
        public async Task<IActionResult> PutLoanAlias([FromBody] LoanAliasParent request)
        {
            if (request is null || request.LoanAliases is null || request.LoanAliases.Count == 0)
            {
                return BadRequest("Request body must contain at least one loan alias.");
            }

            try
            {
                var affected = await _service.LoanAliasUpdate(request);
                return affected > 0 ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update loan aliases cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating loan aliases");
                return StatusCode(500, "An error occurred while updating loan aliases.");
            }
        }

        // PUT: api/loans/{loanKey}
        [HttpPut("{loanKey:int}")]
        public async Task<IActionResult> Put(int loanKey, [FromBody] LoanUpdateRequest request)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            try
            {
                var updated = await _service.UpdateLoanAsync(loanKey, request);
                return updated ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update loan {LoanKey} cancelled", loanKey);
                return StatusCode(499); // Client Closed Request (non-standard) � indicates cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating loan {LoanKey}", loanKey);
                return StatusCode(500, "An error occurred while updating the loan.");
            }
        }

        //// GET: api/funds/{fundKey}
        //[HttpGet("{fundKey:int}")]
        //public async Task<ActionResult<FundDto>> GetByKey(int fundKey)
        //{
        //    try
        //    {
        //        var dto = await _service.GetByKeyAsync(fundKey, cancellationToken);
        //        return dto is null ? NotFound() : Ok(dto);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.LogInformation("Get fund by key {FundKey} cancelled", fundKey);
        //        return StatusCode(499);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error retrieving fund {FundKey}", fundKey);
        //        return StatusCode(500, "An error occurred while retrieving the fund.");
        //    }
        //}
    }
}