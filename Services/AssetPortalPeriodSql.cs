using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

/// <summary>
/// Resolves the warehouse period label passed to asset TVFs
/// (<c>ITD</c> or <c>Q2 2026</c>-style <c>quarter_year</c>).
/// </summary>
internal static class AssetPortalPeriodSql
{
    public const string Itd = "ITD";

    public static async Task<string> ResolvePeriodLabelAsync(
        SqlConnection connection,
        string? period,
        TimeGranularity view,
        int? dateKey)
    {
        if (!string.IsNullOrWhiteSpace(period))
        {
            return period.Trim();
        }

        if (view != TimeGranularity.Quarterly || dateKey is not > 0)
        {
            return Itd;
        }

        await using var command = new SqlCommand(
            $" select top 1 isnull(quarter_year, '') as quarter_year from {WarehouseTables.DimDate} where date_key = @dateKey ",
            connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@dateKey", dateKey.Value);

        var result = await command.ExecuteScalarAsync();
        var label = result is string s ? s.Trim() : result?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(label) ? Itd : label;
    }
}
