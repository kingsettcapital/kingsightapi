using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

internal static class SqlReaderExtensions
{
    private const int RoundedDecimalPlaces = 4;

    public static string GetStringOrEmpty(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    public static string? GetNullableString(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static int GetInt32OrDefault(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    public static long GetInt64OrDefault(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal));
    }

    public static decimal GetDecimalOrDefault(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        var value = reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal));
        return ShouldRoundColumn(column) ? RoundDecimal(value) : value;
    }

    public static decimal? GetNullableDecimal(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = Convert.ToDecimal(reader.GetValue(ordinal));
        return ShouldRoundColumn(column) ? RoundDecimal(value) : value;
    }

    public static DateTime? GetNullableDateTime(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    public static string? GetNullableStringIfPresent(this SqlDataReader reader, string column)
    {
        if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetString(ordinal);
    }

    public static DateTime? GetNullableDateTimeIfPresent(this SqlDataReader reader, string column)
    {
        if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetDateTime(ordinal);
    }

    public static int GetInt32OrDefaultIfPresent(this SqlDataReader reader, string column)
    {
        if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))
        {
            return 0;
        }

        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    public static bool TryGetOrdinal(this SqlDataReader reader, string column, out int ordinal)
    {
        try
        {
            ordinal = reader.GetOrdinal(column);
            return true;
        }
        catch (IndexOutOfRangeException)
        {
            ordinal = -1;
            return false;
        }
    }

    public static string GetStringFromColumns(this SqlDataReader reader, params string[] columns)
    {
        foreach (var column in columns)
        {
            if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))
            {
                continue;
            }

            return reader.GetString(ordinal);
        }

        return string.Empty;
    }

    public static decimal GetDecimalFromColumns(this SqlDataReader reader, params string[] columns)
    {
        foreach (var column in columns)
        {
            if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))
            {
                continue;
            }

            var value = Convert.ToDecimal(reader.GetValue(ordinal));
            return ShouldRoundColumn(column) ? RoundDecimal(value) : value;
        }

        return 0m;
    }

    public static decimal? GetNullableDecimalFromColumns(this SqlDataReader reader, params string[] columns)
    {
        foreach (var column in columns)
        {
            if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))
            {
                continue;
            }

            var value = Convert.ToDecimal(reader.GetValue(ordinal));
            return ShouldRoundColumn(column) ? RoundDecimal(value) : value;
        }

        return null;
    }

    public static long GetInt64FromColumns(this SqlDataReader reader, params string[] columns)
    {
        foreach (var column in columns)
        {
            if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))
            {
                continue;
            }

            return Convert.ToInt64(reader.GetValue(ordinal));
        }

        return 0L;
    }

    public static string MapPropertyStatus(this SqlDataReader reader)
    {
        var raw = reader.GetStringFromColumns("property_status", "property_status_name", "status");
        return raw.Equals("sold", StringComparison.OrdinalIgnoreCase) ? "Sold" : "Active";
    }

    public static IReadOnlyDictionary<string, object?> ToCamelCaseDictionary(this SqlDataReader reader)
    {
        var fields = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(reader.GetName(i));
            fields[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return fields;
    }

    private static decimal RoundDecimal(decimal value) =>
        Math.Round(value, RoundedDecimalPlaces, MidpointRounding.AwayFromZero);

    private static bool ShouldRoundColumn(string column)
    {
        var lower = column.ToLowerInvariant();

        if (lower.Contains("percent") || lower.Contains("yield") || lower.Contains("ltv"))
        {
            return false;
        }

        return lower.Contains("amount")
            || lower.Contains("units")
            || lower.Contains("commitment")
            || lower.Contains("called")
            || lower is "nav"
            || lower.Contains("netinvested")
            || lower.Contains("reserve")
            || lower.Contains("_value")
            || lower.Contains("fmv")
            || lower.Contains("income")
            || lower.Contains("committed")
            || lower.Contains("invested");
    }
}
