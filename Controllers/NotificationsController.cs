using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _service;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            INotificationService service,
            ILogger<NotificationsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET: api/Notifications
        [HttpGet]
        public async Task<ActionResult<List<NotificationDto>>> GetAll(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.GetAllAsync(cancellationToken);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Get notifications cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notifications");
                return StatusCode(500, "An error occurred while retrieving notifications.");
            }
        }

        // PUT: api/Notifications/mark-read
        [HttpPut("mark-read")]
        public async Task<IActionResult> MarkAsRead(
            [FromBody] NotificationMarkReadRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _service.MarkAsReadAsync(request.NotificationIds, cancellationToken);
                return updated ? NoContent() : NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Mark notifications read cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notifications as read");
                return StatusCode(500, "An error occurred while marking notifications as read.");
            }
        }

        // PUT: api/Notifications/mark-all-read
        [HttpPut("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
        {
            try
            {
                await _service.MarkAllAsReadAsync(cancellationToken);
                return NoContent();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Mark all notifications read cancelled");
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                return StatusCode(500, "An error occurred while marking all notifications as read.");
            }
        }
    }
}
