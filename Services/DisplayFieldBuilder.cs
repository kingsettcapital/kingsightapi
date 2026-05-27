using System.Text.Json;
using System.Text.RegularExpressions;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

internal static class DisplayFieldBuilder
{
    public static TypedValueDto Text(string? value) =>
        new() { Value = value ?? string.Empty, FormatType = FieldDataTypes.Text };

    public static TypedValueDto Status(string? value) =>
        new() { Value = value ?? string.Empty, FormatType = FieldDataTypes.Status };

    public static TypedValueDto Phone(string? value) =>
        new() { Value = value ?? string.Empty, FormatType = FieldDataTypes.Phone };

    public static TypedValueDto Money(decimal value) =>
        new() { Value = value, FormatType = FieldDataTypes.Money };

    public static TypedValueDto Money(decimal? value) =>
        new() { Value = value, FormatType = FieldDataTypes.Money };

    public static TypedValueDto Percent(decimal? value) =>
        new() { Value = value, FormatType = FieldDataTypes.Percent };

    public static TypedValueDto Integer(int value) =>
        new() { Value = value, FormatType = FieldDataTypes.Integer };

    public static TypedValueDto Integer(int? value) =>
        new() { Value = value, FormatType = FieldDataTypes.Integer };

    public static TypedValueDto Long(long value) =>
        new() { Value = value, FormatType = FieldDataTypes.Integer };

    public static TypedValueDto Year(int? value) =>
        new() { Value = value, FormatType = FieldDataTypes.Year };

    public static TypedValueDto Date(DateTime? value) =>
        new() { Value = value, FormatType = FieldDataTypes.Date };

    public static TypedValueDto DateTime(DateTime? value) =>
        new() { Value = value, FormatType = FieldDataTypes.DateTime };

    public static TypedValueDto Boolean(bool value) =>
        new() { Value = value, FormatType = FieldDataTypes.Boolean };

    public static TypedValueDto Quarter(string? value) =>
        new() { Value = value ?? string.Empty, FormatType = FieldDataTypes.Quarter };

    public static DynamicFieldDto ToDynamicField(string key, TypedValueDto value) =>
        new()
        {
            Key = key,
            Value = value.Value,
            FormatType = value.FormatType
        };

    public static Dictionary<string, TypedValueDto> DictionaryFromSqlReader(SqlDataReader reader)
    {
        var fields = new Dictionary<string, TypedValueDto>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var key = ToApiKey(columnName);
            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            fields[key] = InferFromColumn(columnName, value);
        }

        return fields;
    }

    private static TypedValueDto InferFromColumn(string columnName, object? value)
    {
        var lower = columnName.ToLowerInvariant();

        if (lower is "property_status" or "investor_status" or "fund_status" || lower.EndsWith("_status"))
        {
            return Status(value?.ToString());
        }

        if (lower is "property_acquisition" or "property_disposition")
        {
            return Quarter(value?.ToString());
        }

        if (lower.Contains("phone") || lower.Contains("telephone") || lower.Contains("mobile") || lower.Contains("cell"))
        {
            return Phone(value?.ToString());
        }

        if (lower.Contains("percent") || lower.Contains("yield") || lower.Contains("return"))
        {
            return Percent(ToNullableDecimal(value));
        }

        if (lower.Contains("amount") || lower.Contains("value") || lower.Contains("fmv") || lower.Contains("income"))
        {
            return Money(ToNullableDecimal(value) ?? 0m);
        }

        if ((lower is "is_current" or "is_sidecar") || lower.StartsWith("is_"))
        {
            return Boolean(ToBoolean(value));
        }

        if (lower.EndsWith("_year") || lower == "join_year")
        {
            return Year(ToNullableInt(value));
        }

        if (lower.EndsWith("_date")
            || lower is "member_since" or "valid_from" or "valid_to"
            || lower.EndsWith("_refreshed_date")
            || lower.EndsWith("_created_date"))
        {
            return value is DateTime dt && dt.TimeOfDay == TimeSpan.Zero
                ? Date(dt)
                : DateTime(ToNullableDateTime(value));
        }

        if (lower.EndsWith("_key") || lower.EndsWith("_id") || lower.EndsWith("_count"))
        {
            return Integer(ToNullableInt(value) ?? 0);
        }

        return value switch
        {
            null => Text(null),
            bool b => Boolean(b),
            DateTime dt => DateTime(dt),
            decimal d => Money(d),
            double d => Money(Convert.ToDecimal(d)),
            float f => Money(Convert.ToDecimal(f)),
            int n => Integer(n),
            long n => Long(n),
            _ => Text(value.ToString())
        };
    }

    private static decimal? ToNullableDecimal(object? value) =>
        value is null or DBNull ? null : Convert.ToDecimal(value);

    private static int? ToNullableInt(object? value) =>
        value is null or DBNull ? null : Convert.ToInt32(value);

    private static DateTime? ToNullableDateTime(object? value) =>
        value is null or DBNull ? null : Convert.ToDateTime(value);

    private static bool ToBoolean(object? value) =>
        value is not null and not DBNull && Convert.ToBoolean(value);

    private static string ToApiKey(string columnName)
    {
        if (columnName.Contains('_'))
        {
            return Regex.Replace(
                columnName.ToLowerInvariant(),
                "_([a-z])",
                m => m.Groups[1].Value.ToUpperInvariant());
        }

        return JsonNamingPolicy.CamelCase.ConvertName(columnName);
    }
}
