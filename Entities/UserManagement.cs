using System.Text.Json.Serialization;

namespace kingsightapi.Entities
{
    /// <summary>Role row for list/detail responses. Ids are sequential integers (1, 2, 3…).</summary>
    public sealed class RoleDto
    {
        [JsonPropertyName("roleId")]
        public int RoleId { get; init; }
        [JsonPropertyName("roleName")]
        public string RoleName { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string? Status { get; init; }
    }

    /// <summary>Create role. Server assigns roleId; do not send roleId in the body.</summary>
    public sealed class RoleSaveRequest
    {
        [JsonPropertyName("roleName")]
        public string RoleName { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string? Status { get; init; }
    }

    /// <summary>Update role. roleId is in the URL path only.</summary>
    public sealed class RoleUpdateRequest
    {
        [JsonPropertyName("roleName")]
        public string RoleName { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string? Status { get; init; }
    }

    /// <summary>User row for list/detail/create/update responses. Ids are sequential integers (1, 2, 3…).</summary>
    public sealed class UserDto
    {
        [JsonPropertyName("userId")]
        public int UserId { get; init; }

        [JsonPropertyName("email")]
        public string Email { get; init; } = string.Empty;

        [JsonPropertyName("firstName")]
        public string? FirstName { get; init; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; init; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; init; }

        [JsonPropertyName("dateCreated")]
        public DateTime DateCreated { get; init; }

        [JsonPropertyName("dateModified")]
        public DateTime? DateModified { get; init; }

        [JsonPropertyName("roleId")]
        public int RoleId { get; init; }

        [JsonPropertyName("roleName")]
        public string RoleName { get; init; } = string.Empty;
    }

    /// <summary>Create user. Server assigns userId; do not send userId in the body.</summary>
    public sealed class UserSaveRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; init; } = string.Empty;

        [JsonPropertyName("firstName")]
        public string? FirstName { get; init; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; init; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; init; } = true;

        [JsonPropertyName("roleId")]
        public int RoleId { get; init; }
    }

    /// <summary>Update user. userId is in the URL path only.</summary>
    public sealed class UserUpdateRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; init; } = string.Empty;

        [JsonPropertyName("firstName")]
        public string? FirstName { get; init; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; init; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; init; } = true;

        [JsonPropertyName("roleId")]
        public int RoleId { get; init; }
    }
}
