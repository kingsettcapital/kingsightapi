namespace kingsightapi.Entities;

/// <summary>Typed value for UI rendering.</summary>
public sealed class TypedValueDto
{
    public object? Value { get; init; }
    public string FormatType { get; init; } = FieldDataTypes.Text;
}
