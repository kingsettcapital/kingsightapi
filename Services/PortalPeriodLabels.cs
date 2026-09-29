namespace kingsightapi.Services;

/// <summary>Display labels for inception-to-date period rows in granular tables.</summary>
internal static class PortalPeriodLabels
{
    public const string InceptionToDate = "ITD";

    public static bool IsInceptionToDate(string? period) =>
        !string.IsNullOrWhiteSpace(period)
        && (string.Equals(period, InceptionToDate, StringComparison.OrdinalIgnoreCase)
            || string.Equals(period, "LTD", StringComparison.OrdinalIgnoreCase)
            || string.Equals(period, "Life To Date", StringComparison.OrdinalIgnoreCase));
}
