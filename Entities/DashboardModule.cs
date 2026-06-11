namespace kingsightapi.Entities;

/// <summary>
/// Top-level Kingsight dashboard modules (Investors / Investments / Assets tabs).
/// API values: <c>investors</c>, <c>investments</c>, <c>assets</c>.
/// </summary>
public enum DashboardModule
{
    Investors,
    Investments,
    Assets
}

/// <summary>Shared constants and helpers for <see cref="DashboardModule"/>.</summary>
public static class DashboardModules
{
    public const string QueryValues = "investors, investments, assets";

    public static readonly IReadOnlyList<DashboardModule> All =
    [
        DashboardModule.Investors,
        DashboardModule.Investments,
        DashboardModule.Assets
    ];

    public static bool TryParseFromApi(string? value, out DashboardModule module)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            module = default;
            return false;
        }

        if (!Enum.TryParse(value.Trim(), ignoreCase: true, out module))
        {
            return false;
        }

        return Enum.IsDefined(module);
    }

    public static string ToApiString(DashboardModule module) =>
        module switch
        {
            DashboardModule.Investors => "investors",
            DashboardModule.Investments => "investments",
            DashboardModule.Assets => "assets",
            _ => throw new ArgumentOutOfRangeException(nameof(module), module, "Unsupported dashboard module.")
        };
}
