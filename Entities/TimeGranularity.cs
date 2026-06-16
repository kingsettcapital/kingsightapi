namespace kingsightapi.Entities;

/// <summary>
/// Reporting time granularity (LTD / Quarterly / Daily toggles).
/// API values: <c>ltd</c>, <c>quarterly</c>, <c>daily</c> (camelCase).
/// </summary>
public enum TimeGranularity
{
    Ltd,
    Quarterly,
    Daily
}

/// <summary>Shared constants and helpers for <see cref="TimeGranularity"/>.</summary>
public static class TimeGranularities
{
    public const string QueryParameterName = "view";
    public const string QueryValues = "ltd, quarterly, daily";

    public static readonly IReadOnlyList<TimeGranularity> All =
    [
        TimeGranularity.Ltd,
        TimeGranularity.Quarterly,
        TimeGranularity.Daily
    ];

    public static bool TryParseFromApi(string? value, out TimeGranularity granularity)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            granularity = default;
            return false;
        }

        if (!Enum.TryParse(value.Trim(), ignoreCase: true, out granularity))
        {
            return false;
        }

        return Enum.IsDefined(granularity);
    }

    public static string ToApiString(TimeGranularity granularity) =>
        granularity switch
        {
            TimeGranularity.Ltd => "ltd",
            TimeGranularity.Quarterly => "quarterly",
            TimeGranularity.Daily => "daily",
            _ => throw new ArgumentOutOfRangeException(nameof(granularity), granularity, "Unsupported time granularity.")
        };
}
