using System.Text.Json.Serialization;

namespace kingsightapi.Entities;

public sealed class FundDocumentsResultDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = "Interim/Annual Reports";

    [JsonPropertyName("folder_path")]
    public string FolderPath { get; init; } = string.Empty;

    [JsonPropertyName("items")]
    public IReadOnlyList<FundDocumentItemDto> Items { get; init; } = [];
}

public sealed class FundDocumentItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("year")]
    public int? Year { get; init; }

    [JsonPropertyName("quarter")]
    public string? Quarter { get; init; }

    [JsonPropertyName("modified_on")]
    public DateTime? ModifiedOn { get; init; }

    [JsonPropertyName("modified_by")]
    public string? ModifiedBy { get; init; }

    [JsonPropertyName("size_bytes")]
    public long? SizeBytes { get; init; }

    [JsonPropertyName("web_url")]
    public string? WebUrl { get; init; }

    [JsonPropertyName("server_relative_url")]
    public string? ServerRelativeUrl { get; init; }
}

public sealed class UpsertFundSharePointLibraryRequest
{
    [JsonPropertyName("sharepoint_url")]
    public string SharePointUrl { get; init; } = string.Empty;
}
