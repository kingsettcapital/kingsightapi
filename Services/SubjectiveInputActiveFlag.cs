namespace kingsightapi.Services;

internal static class SubjectiveInputActiveFlag
{
    public static bool FromDbValue(object? value)
    {
        if (value is null || value is DBNull)
        {
            return false;
        }

        if (value is bool boolean)
        {
            return boolean;
        }

        var text = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return text.Equals("Y", StringComparison.OrdinalIgnoreCase)
            || text.Equals("1", StringComparison.OrdinalIgnoreCase)
            || text.Equals("T", StringComparison.OrdinalIgnoreCase)
            || text.Equals("A", StringComparison.OrdinalIgnoreCase)
            || text.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
    }

    public static string ToDbValue(bool isActive) => isActive ? "Y" : "N";
}
