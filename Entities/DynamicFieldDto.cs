namespace kingsightapi.Entities;

/// <summary>Dynamic card field for overview sections.</summary>
public sealed class DynamicFieldDto
{
    public string Key { get; init; } = string.Empty;
    public object? Value { get; init; }
    public string FormatType { get; init; } = FieldDataTypes.Text;
}
