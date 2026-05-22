namespace kingsightapi.Entities
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Row from mort.dim_investor for API responses.
    /// </summary>
    public sealed class DimInvestorDto
    {
        public long investor_key { get; init; }
        public string investor_code { get; init; } = string.Empty;
        public string investor_name { get; init; } = string.Empty;
        public string investor_alias_name { get; init; } = string.Empty;
        public string? user_updated_by { get; init; } = string.Empty;
        public DateTime? user_updated_date { get; init; } = DateTime.MinValue;
    }

    /// <summary>
    /// One investor row for batch upsert via PUT /api/investor/aliases.
    /// </summary>
    public sealed class DimInvestorAliasUpdateItem
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long investor_key { get; init; }
        public string investor_code { get; init; } = string.Empty;
        public string investor_name { get; init; } = string.Empty;
        public string investor_alias_name { get; init; } = string.Empty;
        public DateTime? user_updated_date { get; init; }
        public string? user_updated_by { get; init; } = string.Empty;
    }

    /// <summary>
    /// Body for batch-updating investor_alias_name on mort.dim_investor.
    /// </summary>
    public sealed class DimInvestorAliasBatchUpdateRequest
    {
        [JsonPropertyName("investors")]
        public List<DimInvestorAliasUpdateItem> Investors { get; init; } = new();
    }

    /// <summary>
    /// Outcome of a batch investor alias upsert.
    /// </summary>
    public sealed class DimInvestorAliasBatchUpdateResult
    {
        public int UpdatedCount { get; init; }
        public int InsertedCount { get; init; }
        public IReadOnlyList<long> FailedInvestorKeys { get; init; } = Array.Empty<long>();
    }

    /// <summary>
    /// Investor name option for the filter multi-select (mort.dim_investor).
    /// </summary>
    public sealed class InvestorNameOptionDto
    {
        public long investor_key { get; init; }
        public string investor_code { get; init; } = string.Empty;
        public string investor_name { get; init; } = string.Empty;
    }
}
