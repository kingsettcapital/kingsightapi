using System.Text.Json.Serialization;

namespace kingsightapi.Entities;

/// <summary>
/// Portal entity type strings for global search and Angular routing.
/// API values: <c>investors</c>, <c>investments</c>, <c>assets</c>.
/// </summary>
public static class PortalEntityTypes
{
    public const string Investors = "investors";
    public const string Investments = "investments";
    public const string Assets = "assets";
}

/// <summary>Single hit from the Kingsight header global search.</summary>
public sealed class GlobalSearchResultDto
{
    /// <summary>Portal route segment: investors | investments | assets.</summary>
    [JsonPropertyName("entity_type")]
    public string EntityType { get; init; } = string.Empty;

    [JsonPropertyName("entity_key")]
    public long EntityKey { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Secondary line for the dropdown (type, AUM, geography, etc.).</summary>
    public string Subtitle { get; init; } = string.Empty;
}

public sealed class GlobalSearchResponseDto
{
    public string Search { get; init; } = string.Empty;
    public IReadOnlyList<GlobalSearchResultDto> Results { get; init; } = [];
}
