using System.Security.Claims;
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
        private readonly IUserService _userService;
        private readonly ILogger<CmhcUploadController> _logger;

        public CmhcUploadController(
            ICmhcUploadService service,
            IUserService userService,
            ILogger<CmhcUploadController> logger)
        {
            _service = service;
            _userService = userService;
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

        // POST: api/CmhcUpload — uploadedByUserId maps to input.UserMst.UserId (server validates against JWT email)
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(62_914_560)]
        [RequestFormLimits(MultipartBodyLengthLimit = 62_914_560)]
        public async Task<ActionResult<CmhcUploadHistoryDto>> Upload(
            [FromForm] CmhcUploadFormRequest request,
            [FromQuery] string? fileType,
            CancellationToken cancellationToken)
        {
            var file = request.File;
            var resolvedFileName = string.IsNullOrWhiteSpace(request.FileName)
                ? file?.FileName
                : request.FileName;
            var resolvedFileType = CmhcUploadFileTypes.Resolve(
                string.IsNullOrWhiteSpace(request.FileType) ? fileType : request.FileType,
                resolvedFileName);

            try
            {
                if (file is null || file.Length == 0)
                {
                    return BadRequest("Upload file is required.");
                }

                if (!CmhcUploadFileTypes.IsSupported(request.FileType ?? fileType, resolvedFileName))
                {
                    return BadRequest("fileType must be 'cmhc' or 'qr-slides'.");
                }

                var uploadedByUserId = await ResolveUploadedByUserIdAsync(request, cancellationToken);
                if (uploadedByUserId is null)
                {
                    return BadRequest(
                        "Unable to resolve uploading user. Sign in with a Kingsight user account registered in User Management.");
                }

                var result = await _service.UploadAsync(
                    file,
                    resolvedFileName ?? string.Empty,
                    uploadedByUserId.Value,
                    resolvedFileType,
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

        private async Task<int?> ResolveUploadedByUserIdAsync(
            CmhcUploadFormRequest request,
            CancellationToken cancellationToken)
        {
            var jwtEmail = GetCurrentUserEmail();
            UserDto? jwtUser = null;
            if (!string.IsNullOrWhiteSpace(jwtEmail))
            {
                jwtUser = await _userService.GetByEmailAsync(jwtEmail, cancellationToken);
            }

            if (jwtUser is not null)
            {
                if (request.UploadedByUserId is > 0 && request.UploadedByUserId != jwtUser.UserId)
                {
                    _logger.LogWarning(
                        "Ignoring client uploadedByUserId {ClientUserId}; JWT user {JwtUserId} ({Email}) will be used.",
                        request.UploadedByUserId,
                        jwtUser.UserId,
                        jwtUser.Email);
                }

                return jwtUser.UserId;
            }

            if (request.UploadedByUserId is > 0)
            {
                var clientUser = await _userService.GetByIdAsync(request.UploadedByUserId.Value, cancellationToken);
                return clientUser?.UserId;
            }

            return null;
        }

        private string? GetCurrentUserEmail() =>
            User.FindFirstValue("preferred_username")
            ?? User.FindFirstValue(ClaimTypes.Upn)
            ?? User.Identity?.Name;
    }
}
