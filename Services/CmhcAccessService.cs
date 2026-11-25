using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services
{
    public interface ICmhcAccessService
    {
        Task<IReadOnlyList<CmhcUserAccessDto>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<CmhcUserAccessDto?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

        Task<CmhcUserAccessDto> SaveAsync(
            int userId,
            CmhcUserAccessSaveRequest request,
            string? updatedBy,
            CancellationToken cancellationToken = default);

        /// <summary>Access for the signed-in user (JWT email → user_master → cmhc_user_access).</summary>
        Task<CmhcMyAccessDto> GetCurrentAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// CMHC dashboard sub-roles stored in subjective_input.cmhc_user_access
    /// (see Scripts/Create_cmhc_user_access.sql).
    /// </summary>
    public sealed class CmhcAccessService : ICmhcAccessService
    {
        /// <summary>SQL Server "invalid object name": the table hasn't been created yet.</summary>
        private const int InvalidObjectNameError = 208;

        private readonly string _connectionString;
        private readonly string _table;
        private readonly IUserService _userService;
        private readonly ICurrentUserResolver _currentUserResolver;
        private readonly ILogger<CmhcAccessService> _logger;

        public CmhcAccessService(
            IConfiguration configuration,
            FabricWarehouseTables tables,
            IUserService userService,
            ICurrentUserResolver currentUserResolver,
            ILogger<CmhcAccessService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _table = tables.SubjectiveInput("cmhc_user_access");
            _userService = userService;
            _currentUserResolver = currentUserResolver;
            _logger = logger;
        }

        public async Task<IReadOnlyList<CmhcUserAccessDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var sql = $"select user_id, cmhc_roles, linked_name, updated_by, updated_datetime from {_table} order by user_id";
            var list = new List<CmhcUserAccessDto>();
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(MapRow(reader));
                }
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectNameError)
            {
                _logger.LogWarning("{Table} does not exist yet; run Scripts/Create_cmhc_user_access.sql.", _table);
            }

            return list;
        }

        public async Task<CmhcUserAccessDto?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            var sql = $"""
                select top 1 user_id, cmhc_roles, linked_name, updated_by, updated_datetime
                from {_table}
                where user_id = @user_id
                order by updated_datetime desc
                """;
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@user_id", userId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectNameError)
            {
                _logger.LogWarning("{Table} does not exist yet; run Scripts/Create_cmhc_user_access.sql.", _table);
                return null;
            }
        }

        public async Task<CmhcUserAccessDto> SaveAsync(
            int userId,
            CmhcUserAccessSaveRequest request,
            string? updatedBy,
            CancellationToken cancellationToken = default)
        {
            var roles = CmhcRoles.Parse(request.Roles);
            var linkedName = string.IsNullOrWhiteSpace(request.LinkedName) ? null : request.LinkedName.Trim();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await using (var delete = new SqlCommand($"delete from {_table} where user_id = @user_id", connection, transaction))
                {
                    delete.Parameters.AddWithValue("@user_id", userId);
                    await delete.ExecuteNonQueryAsync(cancellationToken);
                }

                if (roles.Count > 0 || linkedName is not null)
                {
                    await using var insert = new SqlCommand(
                        $"""
                        insert into {_table} (user_id, cmhc_roles, linked_name, updated_by, updated_datetime)
                        values (@user_id, @cmhc_roles, @linked_name, @updated_by, sysutcdatetime())
                        """,
                        connection,
                        transaction);
                    insert.Parameters.AddWithValue("@user_id", userId);
                    insert.Parameters.AddWithValue("@cmhc_roles", roles.Count > 0 ? string.Join(",", roles) : DBNull.Value);
                    insert.Parameters.AddWithValue("@linked_name", (object?)linkedName ?? DBNull.Value);
                    insert.Parameters.AddWithValue("@updated_by", (object?)updatedBy ?? DBNull.Value);
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return new CmhcUserAccessDto
            {
                UserId = userId,
                Roles = roles,
                LinkedName = linkedName,
                UpdatedBy = updatedBy,
                UpdatedDatetime = DateTime.UtcNow,
            };
        }

        public async Task<CmhcMyAccessDto> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            var email = _currentUserResolver.GetJwtEmail();
            if (string.IsNullOrWhiteSpace(email))
            {
                return CmhcMyAccessDto.None;
            }

            var user = await _userService.GetByEmailAsync(email, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return CmhcMyAccessDto.None;
            }

            var access = await GetByUserIdAsync(user.UserId, cancellationToken);
            var roles = access?.Roles ?? Array.Empty<string>();
            // App Admins, and CMHC-role users with no sub-role (CMHC section admins), see everything.
            var roleName = (user.RoleName ?? string.Empty).Trim();
            var isCmhcRole = string.Equals(roleName, "CMHC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleName, "CMHC User", StringComparison.OrdinalIgnoreCase);
            var isAdmin = MortgageApproverExtensions.IsAdminRole(user.RoleName)
                || (isCmhcRole && roles.Count == 0);

            return new CmhcMyAccessDto
            {
                IsAdmin = isAdmin,
                Roles = roles,
                LinkedName = access?.LinkedName,
                SeesAllLoans = isAdmin || roles.Contains(CmhcRoles.Reviewer),
                HasAccess = isAdmin || roles.Count > 0,
            };
        }

        private static CmhcUserAccessDto MapRow(SqlDataReader reader) => new()
        {
            UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
            Roles = CmhcRoles.Parse(new[] { reader.GetNullableString("cmhc_roles") }),
            LinkedName = reader.GetNullableString("linked_name")?.Trim(),
            UpdatedBy = reader.GetNullableString("updated_by"),
            UpdatedDatetime = reader.IsDBNull(reader.GetOrdinal("updated_datetime"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("updated_datetime")),
        };
    }
}
