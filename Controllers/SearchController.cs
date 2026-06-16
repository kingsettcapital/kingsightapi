using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kingsightapi.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class SearchController : ControllerBase
{
    private readonly IGlobalSearchService _service;
    private readonly ILogger<SearchController> _logger;

    public SearchController(IGlobalSearchService service, ILogger<SearchController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET: api/search?search=fund&limit=20 — header global search (investors, funds, assets)
    [HttpGet]
    public async Task<ActionResult<GlobalSearchResponseDto>> Get(
        [FromQuery] string? search,
        [FromQuery] int limit = 20)
    {
        try
        {
            return Ok(await _service.SearchAsync(search, limit));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Global search cancelled");
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing global search");
            return StatusCode(500, "An error occurred while performing global search.");
        }
    }
}
