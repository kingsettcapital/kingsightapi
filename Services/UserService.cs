using System.Text.RegularExpressions;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IUserService
    {
        Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<UserDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<UserDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<int> CreateAsync(UserSaveRequest request, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(int userId, UserUpdateRequest request, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int userId, CancellationToken cancellationToken = default);
        string? ValidateSaveRequest(UserSaveRequest request);
        string? ValidateUpdateRequest(UserUpdateRequest request);
    }

    public sealed class UserService : IUserService
    {
        private static readonly Regex EmailPattern = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string ListSql;
        private readonly string GetByEmailSql;
        private readonly string GetByIdSql;
        private readonly string NextIdSql;
        private readonly string InsertSql;
        private readonly string UpdateSql;
        private readonly string DeleteSql;
        private readonly string EmailExistsSql;

        private readonly string _connectionString;
        private readonly IRoleService _roleService;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IConfiguration configuration,
            FabricWarehouseTables tables,
            IRoleService roleService,
            ILogger<UserService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _roleService = roleService;
            _logger = logger;

            var userMaster = tables.SubjectiveInput("user_master");
            var roleMaster = tables.SubjectiveInput("role_master");

            ListSql = $"""
                select u.user_id,
                       u.email,
                       u.first_name,
                       u.last_name,
                       u.is_active,
                       u.created_datetime,
                       u.updated_datetime,
                       u.role_id,
                       role_name = isnull(r.role_name, '')
                from {userMaster} u
                left join {roleMaster} r on u.role_id = r.role_id
                order by u.user_id
                """;

            GetByEmailSql = $"""
                select u.user_id,
                       u.email,
                       u.first_name,
                       u.last_name,
                       u.is_active,
                       u.created_datetime,
                       u.updated_datetime,
                       u.role_id,
                       role_name = isnull(r.role_name, '')
                from {userMaster} u
                left join {roleMaster} r on u.role_id = r.role_id
                where lower(u.email) = lower(@email)
                """;

            GetByIdSql = $"""
                select u.user_id,
                       u.email,
                       u.first_name,
                       u.last_name,
                       u.is_active,
                       u.created_datetime,
                       u.updated_datetime,
                       u.role_id,
                       role_name = isnull(r.role_name, '')
                from {userMaster} u
                left join {roleMaster} r on u.role_id = r.role_id
                where u.user_id = @user_id
                """;

            NextIdSql = $"""
                select isnull(max(user_id), 0) + 1
                from {userMaster}
                """;

            InsertSql = $"""
                insert into {userMaster} (
                    user_id, role_id, email, first_name, last_name, is_active, created_datetime, created_by)
                values (
                    @user_id, @role_id, @email, @first_name, @last_name, @is_active, sysutcdatetime(), @created_by)
                """;

            UpdateSql = $"""
                update {userMaster}
                set email = @email,
                    first_name = @first_name,
                    last_name = @last_name,
                    is_active = @is_active,
                    updated_datetime = sysutcdatetime(),
                    updated_by = @updated_by,
                    role_id = @role_id
                where user_id = @user_id
                """;

            DeleteSql = $"delete from {userMaster} where user_id = @user_id";

            EmailExistsSql = $"""
                select 1 from {userMaster}
                where lower(email) = lower(@email)
                  and user_id <> isnull(@exclude_user_id, -1)
                """;
        }

        public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var rows = new List<UserDto>();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(ListSql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            return rows;
        }

        public async Task<UserDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(GetByIdSql, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
        }

        public async Task<UserDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(GetByEmailSql, connection);
            command.Parameters.AddWithValue("@email", email.Trim());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
        }

        public async Task<int> CreateAsync(UserSaveRequest request, CancellationToken cancellationToken = default)
        {
            var validationError = ValidateSaveRequest(request);
            if (validationError is not null)
            {
                throw new InvalidOperationException(validationError);
            }

            if (!await _roleService.ExistsAsync(request.RoleId, cancellationToken))
            {
                throw new InvalidOperationException($"Role {request.RoleId} was not found.");
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                if (await EmailExistsAsync(connection, transaction, request.Email.Trim(), null, cancellationToken))
                {
                    throw new InvalidOperationException($"A user with email '{request.Email}' already exists.");
                }

                var newId = await GetNextIdAsync(connection, transaction, cancellationToken);

                await using var command = new SqlCommand(InsertSql, connection, transaction);
                command.Parameters.AddWithValue("@user_id", newId);
                AddUserParameters(command, request);
                command.Parameters.AddWithValue("@created_by", "system");

                await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Created user {UserId} ({Email}).", newId, request.Email);
                return newId;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<bool> UpdateAsync(int userId, UserUpdateRequest request, CancellationToken cancellationToken = default)
        {
            var validationError = ValidateUpdateRequest(request);
            if (validationError is not null)
            {
                throw new InvalidOperationException(validationError);
            }

            if (!await _roleService.ExistsAsync(request.RoleId, cancellationToken))
            {
                throw new InvalidOperationException($"Role {request.RoleId} was not found.");
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                if (await EmailExistsAsync(connection, transaction, request.Email.Trim(), userId, cancellationToken))
                {
                    throw new InvalidOperationException($"A user with email '{request.Email}' already exists.");
                }

                await using var command = new SqlCommand(UpdateSql, connection, transaction);
                command.Parameters.AddWithValue("@user_id", userId);
                AddUserParameters(command, request);
                command.Parameters.AddWithValue("@updated_by", "system");

                var affected = await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return affected > 0;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(DeleteSql, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public string? ValidateSaveRequest(UserSaveRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return "Email is required.";
            }

            if (!EmailPattern.IsMatch(request.Email.Trim()))
            {
                return "Email format is invalid.";
            }

            if (request.RoleId <= 0)
            {
                return "RoleId must be a positive integer.";
            }

            return null;
        }

        public string? ValidateUpdateRequest(UserUpdateRequest request) =>
            ValidateSaveRequest(new UserSaveRequest
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                IsActive = request.IsActive,
                RoleId = request.RoleId,
            });

        private async Task<int> GetNextIdAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(NextIdSql, connection, transaction);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        private async Task<bool> EmailExistsAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string email,
            int? excludeUserId,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(EmailExistsSql, connection, transaction);
            command.Parameters.AddWithValue("@email", email);
            command.Parameters.AddWithValue("@exclude_user_id", excludeUserId ?? (object)DBNull.Value);
            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }

        private static void AddUserParameters(SqlCommand command, UserSaveRequest request)
        {
            command.Parameters.AddWithValue("@email", request.Email.Trim());
            command.Parameters.AddWithValue("@first_name", ToDbValue(request.FirstName?.Trim()));
            command.Parameters.AddWithValue("@last_name", ToDbValue(request.LastName?.Trim()));
            command.Parameters.AddWithValue("@is_active", SubjectiveInputActiveFlag.ToDbValue(request.IsActive));
            command.Parameters.AddWithValue("@role_id", request.RoleId);
        }

        private static void AddUserParameters(SqlCommand command, UserUpdateRequest request) =>
            AddUserParameters(command, new UserSaveRequest
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                IsActive = request.IsActive,
                RoleId = request.RoleId,
            });

        private static UserDto MapRow(SqlDataReader reader) =>
            new()
            {
                UserId = GetInt32(reader, "user_id"),
                Email = GetString(reader, "email"),
                FirstName = GetNullableString(reader, "first_name"),
                LastName = GetNullableString(reader, "last_name"),
                IsActive = SubjectiveInputActiveFlag.FromDbValue(GetRawValue(reader, "is_active")),
                DateCreated = GetDateTime(reader, "created_datetime"),
                DateModified = GetNullableDateTime(reader, "updated_datetime"),
                RoleId = GetInt32(reader, "role_id"),
                RoleName = GetString(reader, "role_name"),
            };

        private static object ToDbValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

        private static object? GetRawValue(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
        }

        private static int GetInt32(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static string GetString(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        private static string? GetNullableString(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var text = reader.GetString(ordinal);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static DateTime GetDateTime(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return DateTime.UtcNow;
            }

            return DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
        }

        private static DateTime? GetNullableDateTime(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal)
                ? null
                : DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
        }
    }
}
