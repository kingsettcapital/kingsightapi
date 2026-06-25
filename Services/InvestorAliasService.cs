using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IInvestorAliasService
    {
        Task<IReadOnlyList<InvestorAliasDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<InvestorAliasDto?> GetByIdAsync(long investorAliasId, CancellationToken cancellationToken = default);
        Task<long> SaveAsync(InvestorAliasSaveRequest request, string auditDisplayName, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(long investorAliasId, InvestorAliasUpdateRequest request, string auditDisplayName, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(long investorAliasId, CancellationToken cancellationToken = default);
    }

    public sealed class InvestorAliasService : IInvestorAliasService
    {
        private readonly string NextIdSql;
        private readonly string DeleteSql;

        private readonly string _connectionString;
        private readonly string _investorAliasMasterTable;
        private readonly ILogger<InvestorAliasService> _logger;

        private bool _schemaProbed;
        private SubjectiveInputMasterAuditColumns _auditColumns = new();

        public InvestorAliasService(IConfiguration configuration, FabricWarehouseTables tables, ILogger<InvestorAliasService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;

            _investorAliasMasterTable = tables.SubjectiveInput("investor_alias_master");

            NextIdSql = $"""
                select isnull(max(investor_alias_id), 0) + 1
                from {_investorAliasMasterTable}
                """;

            DeleteSql = $"""
                delete from {_investorAliasMasterTable}
                where investor_alias_id = @investor_alias_id
                """;
        }

        public async Task<IReadOnlyList<InvestorAliasDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            var rows = new List<InvestorAliasDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(BuildListSql(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation("Retrieved {Count} investor alias rows.", rows.Count);
            return rows;
        }

        public async Task<InvestorAliasDto?> GetByIdAsync(long investorAliasId, CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(BuildGetByIdSql(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@investor_alias_id", investorAliasId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return MapRow(reader);
        }

        public async Task<long> SaveAsync(
            InvestorAliasSaveRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var newId = await GetNextIdAsync(connection, transaction, cancellationToken);
                var auditUtc = DateTime.UtcNow;

                await using var command = new SqlCommand(BuildInsertSql(), connection, transaction)
                {
                    CommandType = System.Data.CommandType.Text
                };

                command.Parameters.AddWithValue("@investor_alias_id", newId);
                command.Parameters.AddWithValue("@investor_alias_name", request.InvestorAliasName);
                _auditColumns.AddInsertParameters(command, auditDisplayName, auditUtc);

                await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Created investor alias row with id {InvestorAliasId} by {AuditUser}.",
                    newId,
                    auditDisplayName);
                return newId;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<bool> UpdateAsync(
            long investorAliasId,
            InvestorAliasUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(BuildUpdateSql(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            command.Parameters.AddWithValue("@investor_alias_id", investorAliasId);
            command.Parameters.AddWithValue("@investor_alias_name", request.InvestorAliasName);
            _auditColumns.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);

            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Updated investor alias row {InvestorAliasId} by {AuditUser}. Rows affected: {AffectedRows}",
                    investorAliasId,
                    auditDisplayName,
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No row updated for investor_alias_id {InvestorAliasId}.", investorAliasId);
            return false;
        }

        public async Task<bool> DeleteAsync(long investorAliasId, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(DeleteSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@investor_alias_id", investorAliasId);

            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Deleted investor alias row {InvestorAliasId}. Rows affected: {AffectedRows}",
                    investorAliasId,
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No row deleted for investor_alias_id {InvestorAliasId}.", investorAliasId);
            return false;
        }

        private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            if (_schemaProbed)
            {
                return;
            }

            _auditColumns = await SubjectiveInputMasterAuditColumns.ProbeAsync(
                _connectionString,
                _investorAliasMasterTable,
                cancellationToken);
            _schemaProbed = true;
        }

        private string BuildSelectColumns()
        {
            var auditColumns = _auditColumns.SelectListColumns()
                .Select(column => column.Trim('[', ']'));
            return string.Join(
                ",\n                       ",
                new[] { "investor_alias_id", "investor_alias_name" }.Concat(auditColumns));
        }

        private string BuildListSql() =>
            $"""
                select {BuildSelectColumns()}
                from {_investorAliasMasterTable}
                order by investor_alias_id
                """;

        private string BuildGetByIdSql() =>
            $"""
                select {BuildSelectColumns()}
                from {_investorAliasMasterTable}
                where investor_alias_id = @investor_alias_id
                """;

        private string BuildInsertSql() =>
            $"""
                insert into {_investorAliasMasterTable} (
                    investor_alias_id,
                    investor_alias_name{_auditColumns.BuildInsertColumnList()})
                values (
                    @investor_alias_id,
                    @investor_alias_name{_auditColumns.BuildInsertValueList()})
                """;

        private string BuildUpdateSql() =>
            $"""
                update {_investorAliasMasterTable}
                set investor_alias_name = @investor_alias_name{_auditColumns.BuildUpdateSetClause()}
                where investor_alias_id = @investor_alias_id
                """;

        private async Task<long> GetNextIdAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(NextIdSql, connection, transaction)
            {
                CommandType = System.Data.CommandType.Text
            };

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }

        private InvestorAliasDto MapRow(SqlDataReader reader)
        {
            DateTime? ReadAuditDate(string? column)
            {
                if (string.IsNullOrWhiteSpace(column))
                {
                    return null;
                }

                if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))
                {
                    return null;
                }

                return DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
            }

            string ReadAuditUser(string? column)
            {
                if (string.IsNullOrWhiteSpace(column))
                {
                    return string.Empty;
                }

                if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))
                {
                    return string.Empty;
                }

                return reader.GetString(ordinal);
            }

            return new InvestorAliasDto
            {
                InvestorAliasId = reader.GetInt64OrDefault("investor_alias_id"),
                InvestorAliasName = reader.GetStringOrEmpty("investor_alias_name"),
                CreatedBy = ReadAuditUser(_auditColumns.ReadCreatedByColumn),
                CreatedDtm = ReadAuditDate(_auditColumns.ReadCreatedDtmColumn),
                UpdatedBy = ReadAuditUser(_auditColumns.ReadUpdatedByColumn),
                UpdatedDtm = ReadAuditDate(_auditColumns.ReadUpdatedDtmColumn)
            };
        }
    }
}
