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

            var userMst = tables.Input("UserMst");
            var roleMst = tables.Input("RoleMst");

            ListSql = $"""
                select u.UserId,
                       u.Email,
                       u.FirstName,
                       u.LastName,
                       u.IsActive,
                       u.DateCreated,
                       u.DateModified,
                       u.RoleId,
                       RoleName = isnull(r.RoleName, '')
                from {userMst} u
                left join {roleMst} r on u.RoleId = r.RoleId
                order by u.UserId
                """;

            GetByEmailSql = $"""
                select u.UserId,
                       u.Email,
                       u.FirstName,
                       u.LastName,
                       u.IsActive,
                       u.DateCreated,
                       u.DateModified,
                       u.RoleId,
                       RoleName = isnull(r.RoleName, '')
                from {userMst} u
                left join {roleMst} r on u.RoleId = r.RoleId
                where lower(u.Email) = lower(@email)
                """;

            GetByIdSql = $"""
                select u.UserId,
                       u.Email,
                       u.FirstName,
                       u.LastName,
                       u.IsActive,
                       u.DateCreated,
                       u.DateModified,
                       u.RoleId,
                       RoleName = isnull(r.RoleName, '')
                from {userMst} u
                left join {roleMst} r on u.RoleId = r.RoleId
                where u.UserId = @user_id
                """;

            NextIdSql = $"""
                select isnull(max(UserId), 0) + 1
                from {userMst}
                """;

            InsertSql = $"""
                insert into {userMst} (
                    UserId, Email, FirstName, LastName, IsActive, DateCreated, DateModified, RoleId)
                values (
                    @user_id, @email, @first_name, @last_name, @is_active, sysutcdatetime(), null, @role_id)
                """;

            UpdateSql = $"""
                update {userMst}
                set Email = @email,
                    FirstName = @first_name,
                    LastName = @last_name,
                    IsActive = @is_active,
                    DateModified = sysutcdatetime(),
                    RoleId = @role_id
                where UserId = @user_id
                """;

            DeleteSql = $"delete from {userMst} where UserId = @user_id";

            EmailExistsSql = $"""
                select 1 from {userMst}
                where lower(Email) = lower(@email)
                  and UserId <> isnull(@exclude_user_id, -1)
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
                RoleId = request.RoleId
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
            command.Parameters.AddWithValue("@is_active", request.IsActive);
            command.Parameters.AddWithValue("@role_id", request.RoleId);
        }

        private static void AddUserParameters(SqlCommand command, UserUpdateRequest request) =>
            AddUserParameters(command, new UserSaveRequest
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                IsActive = request.IsActive,
                RoleId = request.RoleId
            });

        private static UserDto MapRow(SqlDataReader reader) =>
            new()
            {
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                Email = GetString(reader, "Email"),
                FirstName = GetNullableString(reader, "FirstName"),
                LastName = GetNullableString(reader, "LastName"),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                DateModified = GetNullableDateTime(reader, "DateModified"),
                RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                RoleName = GetString(reader, "RoleName")
            };

        private static object ToDbValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

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

        private static DateTime? GetNullableDateTime(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
    }
}
