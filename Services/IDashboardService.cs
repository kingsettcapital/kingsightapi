using kingsightapi.Entities;

namespace kingsightapi.Services;

public interface IDashboardService
{
    /// <summary>Returns dashboard widget payloads for 1–5 widget ids (validated by the controller).</summary>
    Task<DashboardResponseDto> GetDashboardAsync(
        int calendarYear,
        IReadOnlyList<string> widgetIds,
        CancellationToken cancellationToken = default);
}
