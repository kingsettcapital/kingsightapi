using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CmhcUploadController : ControllerBase
    {
        private readonly ICmhcUploadService _service;
        private readonly ILogger<CmhcUploadController> _logger;

        public CmhcUploadController(ICmhcUploadService service, ILogger<CmhcUploadController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET: api/CmhcUpload/history
        [HttpGet("history")]
        public async Task<ActionResult<List<CmhcUploadHistoryDto>>> GetHistory(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.GetHistoryAsync(cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get CMHC upload history cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving CMHC upload history");
                return StatusCode(500, "An error occurred while retrieving CMHC upload history.");
            }
        }

        // POST: api/CmhcUpload — uploadedBy must be a user GUID (maps to UNIQUEIDENTIFIER)
        [HttpPost]
        [RequestSizeLimit(52_428_800)]
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
        public async Task<ActionResult<CmhcUploadHistoryDto>> Upload(
            IFormFile? file,
            [FromForm] string? fileName,
            [FromForm] string? uploadedBy,
            CancellationToken cancellationToken)
        {
            var resolvedFileName = string.IsNullOrWhiteSpace(fileName)
                ? file?.FileName
                : fileName;

            try
            {
                if (file is null || file.Length == 0)
                {
                    return BadRequest("Upload file is required.");
                }

                var result = await _service.UploadAsync(
                    file,
                    resolvedFileName ?? string.Empty,
                    uploadedBy ?? string.Empty,
                    cancellationToken);

                return Created("/api/CmhcUpload/history", result);
            }
            catch (CmhcUploadValidationException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("CMHC upload cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading CMHC file {FileName}", resolvedFileName);
                return StatusCode(500, "An error occurred while uploading the CMHC file.");
            }
        }

        // GET: api/CmhcUpload/template
        [HttpGet("template")]
        public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
        {
            try
            {
                var (stream, downloadName) = await _service.GetTemplateAsync(cancellationToken);
                return File(
                    stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    downloadName);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "CMHC template file not found");
                return NotFound("CMHC upload template file was not found.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("CMHC template download cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading CMHC template");
                return StatusCode(500, "An error occurred while downloading the CMHC template.");
            }
        }
    }
}
