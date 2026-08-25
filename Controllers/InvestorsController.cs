using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvestorsController : ControllerBase
    {
        private readonly IInvestorService _service;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly IUserService _userService;
        private readonly ILogger<InvestorsController> _logger;

        public InvestorsController(
            IInvestorService service,
            ICurrentUserResolver currentUserResolver,
            IUserService userService,
            ILogger<InvestorsController> logger)
        {
            _service = service;
            _currentUserResolver = currentUserResolver;
            _userService = userService;
            _logger = logger;
        }

        // GET: api/Investors
        [HttpGet]
        public async Task<ActionResult<List<InvestorDto>>> GetAll()
        {
            try
            {
                var result = await _service.GetAllAsync();
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get all investor rows cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving investor rows");
                return StatusCode(500, "An error occurred while retrieving investor rows.");
            }
        }

        // POST: api/Investors
        [HttpPost]
        public async Task<ActionResult<InvestorDto>> Create(
            [FromBody] InvestorCreateRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.InvestorName))
            {
                return BadRequest("Investor name is required.");
            }

            var (auditDisplayName, auditError) = await _currentUserResolver.RequireAuditDisplayNameAsync(
                request.CreatedBy,
                "createdBy",
                cancellationToken);
            if (auditError is not null)
            {
                return auditError;
            }

            try
            {
                var created = await _service.CreateAsync(request, auditDisplayName!, cancellationToken);
                return StatusCode(StatusCodes.Status201Created, created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Create investor validation failed");
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Create investor cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating investor");
                return StatusCode(500, "An error occurred while creating the investor.");
            }
        }

        // PUT: api/Investors
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] InvestorUpdateBatchRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            foreach (var investor in request.Investors)
            {
                if (!investor.InvestorAliasKey.HasValue)
                {
                    return BadRequest("Investor alias key is required.");
                }

                if (string.IsNullOrWhiteSpace(investor.InvestorCode))
                {
                    return BadRequest("Investor code is required.");
                }
            }

            var superUserError = await _currentUserResolver.RequireMortgageSuperUserAsync(
                _userService,
                cancellationToken);
            if (superUserError is not null)
            {
                return superUserError;
            }

            var clientAudit = request.Investors.FirstOrDefault()?.UserUpdatedBy;
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
                var updated = await _service.UpdateAsync(request, auditDisplayName!);
                return updated ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Update investor cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating investor row");
                return StatusCode(500, "An error occurred while updating the investor row.");
            }
        }
    }
}
