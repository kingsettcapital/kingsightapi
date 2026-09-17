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

/// <summary>
/// Asset financial metrics ownership basis for
/// <c>fn_asset_financial_ks</c> vs <c>fn_asset_financial_100pct</c>.
/// API values: <c>ks</c>, <c>full</c> / <c>at100</c> / <c>100</c>.
/// </summary>
public enum AssetFinancialShareBasis
{
    Ks,
    At100Pct
}

public static class AssetFinancialShareBases
{
    public static bool TryParseFromApi(string? value, out AssetFinancialShareBasis basis)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            basis = AssetFinancialShareBasis.Ks;
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "ks":
            case "share":
            case "at-share":
            case "atshare":
                basis = AssetFinancialShareBasis.Ks;
                return true;
            case "full":
            case "100":
            case "at100":
            case "at-100":
            case "100pct":
            case "at100pct":
                basis = AssetFinancialShareBasis.At100Pct;
                return true;
            default:
                basis = default;
                return false;
        }
    }
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
