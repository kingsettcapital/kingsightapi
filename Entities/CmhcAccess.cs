using System.Text.Json.Serialization;

namespace kingsightapi.Entities
{
    /// <summary>CMHC dashboard sub-roles. Admin is the app-level role from user_master.</summary>
    public static class CmhcRoles
    {
        public const string Underwriter = "Underwriter";
        public const string Reviewer = "Reviewer";
        public const string Originator = "Originator";

        public static readonly IReadOnlyList<string> All = new[] { Underwriter, Reviewer, Originator };

        /// <summary>Parses a comma list into canonical role names, dropping unknown values and duplicates.</summary>
        public static IReadOnlyList<string> Parse(IEnumerable<string?>? values) =>
            (values ?? Array.Empty<string?>())
                .SelectMany(v => (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(v => All.FirstOrDefault(r => string.Equals(r, v, StringComparison.OrdinalIgnoreCase)))
                .Where(v => v is not null)
                .Select(v => v!)
                .Distinct()
                .OrderBy(v => All.ToList().IndexOf(v))
                .ToList();
    }

    /// <summary>One user's CMHC sub-roles (User Management).</summary>
    public sealed class CmhcUserAccessDto
    {
        [JsonPropertyName("userId")]
        public int UserId { get; init; }

        [JsonPropertyName("roles")]
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

        /// <summary>Name as it appears in the loan data (Funder/Underwriter, or Current/Originating Originator).</summary>
        [JsonPropertyName("linkedName")]
        public string? LinkedName { get; init; }

        [JsonPropertyName("updatedBy")]
        public string? UpdatedBy { get; init; }

        [JsonPropertyName("updatedDatetime")]
        public DateTime? UpdatedDatetime { get; init; }
    }

    public sealed class CmhcUserAccessSaveRequest
    {
        [JsonPropertyName("roles")]
        public IReadOnlyList<string>? Roles { get; init; }

        [JsonPropertyName("linkedName")]
        public string? LinkedName { get; init; }
    }

    /// <summary>What the signed-in user may see on the CMHC dashboard.</summary>
    public sealed class CmhcMyAccessDto
    {
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; init; }

        [JsonPropertyName("roles")]
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

        [JsonPropertyName("linkedName")]
        public string? LinkedName { get; init; }

        /// <summary>Admin or Reviewer: the pipeline is not filtered.</summary>
        [JsonPropertyName("seesAllLoans")]
        public bool SeesAllLoans { get; init; }

        /// <summary>False when the user has no CMHC sub-role (and is not Admin).</summary>
        [JsonPropertyName("hasAccess")]
        public bool HasAccess { get; init; }

        [JsonIgnore]
        public bool IsUnderwriter => Roles.Contains(CmhcRoles.Underwriter);

        [JsonIgnore]
        public bool IsOriginator => Roles.Contains(CmhcRoles.Originator);

        public static CmhcMyAccessDto None { get; } = new();
    }
}
