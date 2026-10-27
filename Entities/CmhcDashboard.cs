using System.Text.Json;

namespace kingsightapi.Entities
{
    /// <summary>
    /// Grid shaped like the Excel parse the CMHC report expects: header row plus value rows.
    /// </summary>
    public sealed class CmhcPipelineDto
    {
        public string AsOf { get; set; } = string.Empty;
        public DateTimeOffset GeneratedAt { get; set; }
        public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
        public IReadOnlyList<object?[]> Rows { get; set; } = Array.Empty<object?[]>();
        public IReadOnlyList<string?> Links { get; set; } = Array.Empty<string?>();
    }

    public sealed class CmhcReportStateDto
    {
        public string Key { get; set; } = string.Empty;

        /// <summary>Stored JSON; null when the key has never been saved.</summary>
        public JsonElement? Value { get; set; }

        /// <summary>0 when the key has never been saved.</summary>
        public long Version { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public sealed class CmhcReportStateWriteRequest
    {
        public JsonElement Value { get; set; }

        /// <summary>Version last read by the client (0 for a first save).</summary>
        public long Version { get; set; }
    }

    public enum CmhcReportStateWriteStatus
    {
        Saved,
        Conflict
    }

    public sealed class CmhcReportStateWriteResult
    {
        public CmhcReportStateWriteStatus Status { get; set; }
        public CmhcReportStateDto? State { get; set; }
    }
}
