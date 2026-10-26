using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Controllers
{
    /// <summary>Property photo per loan alias, shown on the Loan Detail report.</summary>
    [ApiController]
    [Route("api/LoanAlias/{loanAliasId:long}/property-image")]
    public class PropertyImageController : ControllerBase
    {
        private const long MaxRequestBytes = 10_485_760 + 65_536;

        private readonly IPropertyImageService _service;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly ILogger<PropertyImageController> _logger;

        public PropertyImageController(
            IPropertyImageService service,
            ICurrentUserResolver currentUserResolver,
            ILogger<PropertyImageController> logger)
        {
            _service = service;
            _currentUserResolver = currentUserResolver;
            _logger = logger;
        }

        // GET: api/LoanAlias/22/property-image/info  (204 when no image)
        [HttpGet("info")]
        public async Task<ActionResult<PropertyImageDto>> GetInfo(long loanAliasId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.GetActiveAsync(loanAliasId, cancellationToken);
                return result is null ? NoContent() : Ok(result);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving property image info for loan alias {LoanAliasId}", loanAliasId);
                return StatusCode(500, "An error occurred while retrieving the property image.");
            }
        }

        // GET: api/LoanAlias/22/property-image  (image bytes; 404 when no image)
        [HttpGet]
        public async Task<IActionResult> Get(long loanAliasId, CancellationToken cancellationToken)
        {
            try
            {
                var image = await _service.OpenActiveAsync(loanAliasId, cancellationToken);
                if (image is null)
                {
                    return NotFound();
                }

                Response.Headers.CacheControl = "private, no-cache";
                return File(image.Value.Stream, image.Value.ContentType);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving property image for loan alias {LoanAliasId}", loanAliasId);
                return StatusCode(500, "An error occurred while retrieving the property image.");
            }
        }

        // POST: api/LoanAlias/22/property-image  (multipart: file, updatedBy)
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<ActionResult<PropertyImageDto>> Upload(
            long loanAliasId,
            IFormFile? file,
            [FromForm] string? updatedBy,
            CancellationToken cancellationToken)
        {
            if (loanAliasId <= 0)
            {
                return BadRequest("Loan alias id is required.");
            }

            if (file is null || file.Length == 0)
            {
                return BadRequest("Image file is required.");
            }

            var (auditDisplayName, auditError) = await _currentUserResolver.RequireAuditDisplayNameAsync(
                updatedBy,
                "updatedBy",
                cancellationToken);
            if (auditError is not null)
            {
                return auditError;
            }

            try
            {
                var result = await _service.UploadAsync(loanAliasId, file, auditDisplayName!, cancellationToken);
                return Created($"/api/LoanAlias/{loanAliasId}/property-image", result);
            }
            catch (CmhcUploadValidationException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading property image for loan alias {LoanAliasId}", loanAliasId);
                return StatusCode(500, "An error occurred while uploading the property image.");
            }
        }

        // DELETE: api/LoanAlias/22/property-image?updatedBy=...
        [HttpDelete]
        public async Task<IActionResult> Delete(
            long loanAliasId,
            [FromQuery] string? updatedBy,
            CancellationToken cancellationToken)
        {
            var (auditDisplayName, auditError) = await _currentUserResolver.RequireAuditDisplayNameAsync(
                updatedBy,
                "updatedBy",
                cancellationToken);
            if (auditError is not null)
            {
                return auditError;
            }

            try
            {
                var removed = await _service.DeactivateAsync(loanAliasId, auditDisplayName!, cancellationToken);
                return removed ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing property image for loan alias {LoanAliasId}", loanAliasId);
                return StatusCode(500, "An error occurred while removing the property image.");
            }
        }
    }
}
