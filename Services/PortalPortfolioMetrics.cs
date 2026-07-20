namespace kingsightapi.Services;

internal static class PortalPortfolioMetrics
{
    public static decimal? ComputeInvestedPercent(decimal commitment, decimal netInvested) =>
        commitment > 0m
            ? Math.Round(netInvested / commitment * 100m, 1, MidpointRounding.AwayFromZero)
            : null;

    public static string FormatContactName(string firstName, string lastName)
    {
        var first = firstName.Trim();
        var last = lastName.Trim();
        if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(last))
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(last))
        {
            return first;
        }

        if (string.IsNullOrEmpty(first))
        {
            return last;
        }

        return $"{first} {last}";
    }
}
