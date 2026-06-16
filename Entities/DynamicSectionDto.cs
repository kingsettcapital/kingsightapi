namespace kingsightapi.Entities;

/// <summary>Dynamic card section with title and fields.</summary>
public sealed class DynamicSectionDto
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<DynamicFieldDto> Fields { get; init; } = [];
}
