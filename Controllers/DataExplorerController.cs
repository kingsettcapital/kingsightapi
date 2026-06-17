using System.Security.Claims;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

/// <summary>
/// Data Explorer tool over <c>view_investor_portfolio_ltd</c>.
/// Columns catalog, dynamic data query, and saved templates (columns + filters + group-by).
/// </summary>
[ApiController]
[Route("api/data-explorer")]
public class DataExplorerController : ControllerBase
{
    private readonly IDataExplorerService _service;
    private readonly ILogger<DataExplorerController> _logger;

    public DataExplorerController(IDataExplorerService service, ILogger<DataExplorerController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET: api/data-explorer/columns
    [HttpGet("columns")]
    public async Task<ActionResult<IReadOnlyList<DataExplorerColumnGroupDto>>> GetColumns()
    {
        try
        {
            var result = await _service.GetColumnsAsync();
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get data explorer columns cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving data explorer columns");
            return StatusCode(500, "An error occurred while retrieving columns.");
        }
    }

    // POST: api/data-explorer/data
    // Body: { columns, filters?, filterLogic?, groupByField?, search?, sortBy?, sortDir?, page?, pageSize? }
    [HttpPost("data")]
    public async Task<ActionResult<DataExplorerDataResult>> GetData([FromBody] DataExplorerDataRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        try
        {
            var result = await _service.GetDataAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get data explorer data cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving data explorer data");
            return StatusCode(500, "An error occurred while retrieving data.");
        }
    }

    // GET: api/data-explorer/templates
    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<DataExplorerTemplateSummaryDto>>> GetTemplates()
    {
        try
        {
            var result = await _service.GetTemplatesAsync();
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get data explorer templates cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving data explorer templates");
            return StatusCode(500, "An error occurred while retrieving templates.");
        }
    }

    // GET: api/data-explorer/templates/{templateId}
    [HttpGet("templates/{templateId}")]
    public async Task<ActionResult<DataExplorerTemplateDto>> GetTemplate(string templateId)
    {
        if (!TryParseTemplateId(templateId, out var id))
        {
            return BadRequest("Invalid template id.");
        }

        try
        {
            var result = await _service.GetTemplateAsync(id);
            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get data explorer template {TemplateId} cancelled", id);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving data explorer template {TemplateId}", id);
            return StatusCode(500, "An error occurred while retrieving the template.");
        }
    }

    // POST: api/data-explorer/templates
    // Body: { name, description?, columns, filters?, filterLogic?, groupByField? }
    [HttpPost("templates")]
    public async Task<ActionResult<DataExplorerTemplateDto>> SaveTemplate([FromBody] DataExplorerSaveTemplateRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        try
        {
            var result = await _service.SaveTemplateAsync(request, GetCurrentUser());
            return CreatedAtAction(nameof(GetTemplate), new { templateId = result.TemplateId.ToString() }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Save data explorer template cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving data explorer template");
            return StatusCode(500, "An error occurred while saving the template.");
        }
    }

    // PUT: api/data-explorer/templates/{templateId}
    [HttpPut("templates/{templateId}")]
    public async Task<ActionResult<DataExplorerTemplateDto>> UpdateTemplate(
        string templateId,
        [FromBody] DataExplorerSaveTemplateRequest request)
    {
        if (!TryParseTemplateId(templateId, out var id))
        {
            return BadRequest("Invalid template id.");
        }

        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        try
        {
            var result = await _service.UpdateTemplateAsync(id, request, GetCurrentUser());
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update data explorer template {TemplateId} cancelled", id);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating data explorer template {TemplateId}", id);
            return StatusCode(500, "An error occurred while updating the template.");
        }
    }

    // DELETE: api/data-explorer/templates/{templateId}
    [HttpDelete("templates/{templateId}")]
    public async Task<IActionResult> DeleteTemplate(string templateId)
    {
        if (!TryParseTemplateId(templateId, out var id))
        {
            return BadRequest("Invalid template id.");
        }

        try
        {
            await _service.DeleteTemplateAsync(id, GetCurrentUser());
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Delete data explorer template {TemplateId} cancelled", id);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting data explorer template {TemplateId}", id);
            return StatusCode(500, "An error occurred while deleting the template.");
        }
    }

    private static bool TryParseTemplateId(string templateId, out long id) =>
        long.TryParse(templateId, out id) && id > 0;

    private string GetCurrentUser() =>
        User.FindFirstValue("preferred_username")
        ?? User.FindFirstValue(ClaimTypes.Upn)
        ?? User.Identity?.Name
        ?? "System";
}
