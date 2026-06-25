using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IRoleService
    {
        Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<RoleDto?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default);
        Task<int> CreateAsync(RoleSaveRequest request, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(int roleId, RoleUpdateRequest request, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int roleId, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(int roleId, CancellationToken cancellationToken = default);
    }

    public sealed class RoleService : IRoleService
    {
        private readonly string ListSql;
        private readonly string GetByIdSql;
        private readonly string NextIdSql;
        private readonly string InsertSql;
        private readonly string UpdateSql;
        private readonly string DeleteSql;
        private readonly string UserCountSql;
        private readonly string ExistsSql;

        private readonly string _connectionString;
        private readonly ILogger<RoleService> _logger;

        public RoleService(IConfiguration configuration, FabricWarehouseTables tables, ILogger<RoleService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;

            var roleMst = tables.Input("RoleMst");
            var userMst = tables.Input("UserMst");

            ListSql = $"""
                select RoleId,
                       RoleName,
                       Status
                from {roleMst}
                order by RoleId
                """;

            GetByIdSql = $"""
                select RoleId,
                       RoleName,
                       Status
                from {roleMst}
                where RoleId = @role_id
                """;

            NextIdSql = $"""
                select isnull(max(RoleId), 0) + 1
                from {roleMst}
                """;

            InsertSql = $"""
                insert into {roleMst} (RoleId, RoleName, Status)
                values (@role_id, @role_name, @status)
                """;

            UpdateSql = $"""
                update {roleMst}
                set RoleName = @role_name,
                    Status = @status
                where RoleId = @role_id
                """;

            DeleteSql = $"""
                delete from {roleMst}
                where RoleId = @role_id
                """;

            UserCountSql = $"""
                select count(*)
                from {userMst}
                where RoleId = @role_id
                """;

            ExistsSql = $"select 1 from {roleMst} where RoleId = @role_id";
        }

        public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var rows = new List<RoleDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(ListSql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation("Retrieved {Count} role rows.", rows.Count);
            return rows;
        }

        public async Task<RoleDto?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(GetByIdSql, connection);
            command.Parameters.AddWithValue("@role_id", roleId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
        }

        public async Task<int> CreateAsync(RoleSaveRequest request, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var newId = await GetNextIdAsync(connection, transaction, cancellationToken);

                await using var command = new SqlCommand(InsertSql, connection, transaction);
                command.Parameters.AddWithValue("@role_id", newId);
                command.Parameters.AddWithValue("@role_name", request.RoleName.Trim());
                command.Parameters.AddWithValue("@status", ToDbValue(NormalizeStatus(request.Status)));

                await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Created role {RoleId} ({RoleName}).", newId, request.RoleName);
                return newId;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<bool> UpdateAsync(
            int roleId,
            RoleUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(UpdateSql, connection);
            command.Parameters.AddWithValue("@role_id", roleId);
            command.Parameters.AddWithValue("@role_name", request.RoleName.Trim());
            command.Parameters.AddWithValue("@status", ToDbValue(NormalizeStatus(request.Status)));

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> DeleteAsync(int roleId, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var countCommand = new SqlCommand(UserCountSql, connection);
            countCommand.Parameters.AddWithValue("@role_id", roleId);
            var userCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
            if (userCount > 0)
            {
                throw new InvalidOperationException(
                    $"Role {roleId} cannot be deleted because {userCount} user(s) are assigned to it.");
            }

            await using var command = new SqlCommand(DeleteSql, connection);
            command.Parameters.AddWithValue("@role_id", roleId);

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> ExistsAsync(int roleId, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(ExistsSql, connection);
            command.Parameters.AddWithValue("@role_id", roleId);

            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }

        private async Task<int> GetNextIdAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(NextIdSql, connection, transaction);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        private static RoleDto MapRow(SqlDataReader reader) =>
            new()
            {
                RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                RoleName = GetString(reader, "RoleName"),
                Status = GetNullableString(reader, "Status")
            };

        private static string? NormalizeStatus(string? status) =>
            string.IsNullOrWhiteSpace(status) ? null : status.Trim()[..1];

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
    }
}
