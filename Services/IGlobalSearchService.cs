using kingsightapi.Entities;

namespace kingsightapi.Services;

public interface IGlobalSearchService
{
    Task<GlobalSearchResponseDto> SearchAsync(string? search, int limit);
}
