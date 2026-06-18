namespace kingsightapi.Entities;

/// <summary>
/// Optional period from the UI: <c>dateKey</c> selects one quarter (via <c>dim_date</c>);
/// <c>calendarYear</c> selects all quarters in a year when no <c>dateKey</c> is sent.
/// </summary>
public sealed class FundPeriodFilter
{
    public int? DateKey { get; init; }

    public int? CalendarYear { get; init; }

    public bool HasDateKey => DateKey is > 0;

    public bool HasCalendarYear => CalendarYear is > 1900;
}
