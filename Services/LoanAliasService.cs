using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface ILoanAliasService
    {
        Task<IReadOnlyList<LoanAliasDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<LoanAliasDto?> GetByIdAsync(long loanAliasId, CancellationToken cancellationToken = default);
        Task<long> SaveAsync(LoanAliasSaveRequest request, string auditDisplayName, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(long loanAliasId, LoanAliasUpdateRequest request, string auditDisplayName, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(long loanAliasId, CancellationToken cancellationToken = default);
    }

    public sealed class LoanAliasService : ILoanAliasService
    {
        private static readonly string[] ReadOnlyMasterColumns =
        [
            "security_value",
            "units",
            "net_acres",
            "square_feet"
        ];

        private readonly string NextIdSql;
        private readonly string DeleteSql;

        private readonly string _connectionString;
        private readonly string _loanAliasMasterTable;
        private readonly ILogger<LoanAliasService> _logger;

        private bool _schemaProbed;
        private SubjectiveInputMasterAuditColumns _auditColumns = new();

        public LoanAliasService(IConfiguration configuration, FabricWarehouseTables tables, ILogger<LoanAliasService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;

            _loanAliasMasterTable = tables.SubjectiveInput("loan_alias_master");

            NextIdSql = $"""
                select isnull(max(loan_alias_id), 0) + 1
                from {_loanAliasMasterTable}
                """;

            DeleteSql = $"""
                delete from {_loanAliasMasterTable}
                where loan_alias_id = @loan_alias_id
                """;
        }

        public async Task<IReadOnlyList<LoanAliasDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            var rows = new List<LoanAliasDto>();

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

            _logger.LogInformation("Retrieved {Count} loan alias rows.", rows.Count);
            return rows;
        }

        public async Task<LoanAliasDto?> GetByIdAsync(long loanAliasId, CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(BuildGetByIdSql(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return MapRow(reader);
        }

        public async Task<long> SaveAsync(
            LoanAliasSaveRequest request,
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

                command.Parameters.AddWithValue("@loan_alias_id", newId);
                command.Parameters.AddWithValue("@loan_alias_name", request.LoanAliasName);
                _auditColumns.AddInsertParameters(command, auditDisplayName, auditUtc);

                await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Created loan alias row with id {LoanAliasId} by {AuditUser}.",
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
            long loanAliasId,
            LoanAliasUpdateRequest request,
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

            command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);
            command.Parameters.AddWithValue("@loan_alias_name", request.LoanAliasName);
            _auditColumns.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);

            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Updated loan alias row {LoanAliasId} by {AuditUser}. Rows affected: {AffectedRows}",
                    loanAliasId,
                    auditDisplayName,
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No row updated for loan_alias_id {LoanAliasId}.", loanAliasId);
            return false;
        }

        public async Task<bool> DeleteAsync(long loanAliasId, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(DeleteSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);

            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Deleted loan alias row {LoanAliasId}. Rows affected: {AffectedRows}",
                    loanAliasId,
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No row deleted for loan_alias_id {LoanAliasId}.", loanAliasId);
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
                _loanAliasMasterTable,
                cancellationToken);
            _schemaProbed = true;
        }

        private string BuildSelectColumns()
        {
            var auditColumns = _auditColumns.SelectListColumns()
                .Select(column => column.Trim('[', ']'));
            return string.Join(
                ",\n                       ",
                new[] { "loan_alias_id", "loan_alias_name" }
                    .Concat(ReadOnlyMasterColumns)
                    .Concat(auditColumns));
        }

        private string BuildListSql() =>
            $"""
                select {BuildSelectColumns()}
                from {_loanAliasMasterTable}
                order by loan_alias_id
                """;

        private string BuildGetByIdSql() =>
            $"""
                select {BuildSelectColumns()}
                from {_loanAliasMasterTable}
                where loan_alias_id = @loan_alias_id
                """;

        private string BuildInsertSql() =>
            $"""
                insert into {_loanAliasMasterTable} (
                    loan_alias_id,
                    loan_alias_name{_auditColumns.BuildInsertColumnList()})
                values (
                    @loan_alias_id,
                    @loan_alias_name{_auditColumns.BuildInsertValueList()})
                """;

        private string BuildUpdateSql() =>
            $"""
                update {_loanAliasMasterTable}
                set loan_alias_name = @loan_alias_name{_auditColumns.BuildUpdateSetClause()}
                where loan_alias_id = @loan_alias_id
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

        private LoanAliasDto MapRow(SqlDataReader reader)
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

            return new LoanAliasDto
            {
                LoanAliasId = reader.GetInt64OrDefault("loan_alias_id"),
                LoanAliasName = reader.GetStringOrEmpty("loan_alias_name"),
                SecurityValue = reader.GetNullableDecimal("security_value"),
                Units = reader.GetNullableInt32("units"),
                NetAcres = reader.GetNullableDecimal("net_acres"),
                SquareFeet = reader.GetNullableDecimal("square_feet"),
                CreatedBy = ReadAuditUser(_auditColumns.ReadCreatedByColumn),
                CreatedDtm = ReadAuditDate(_auditColumns.ReadCreatedDtmColumn),
                UpdatedBy = ReadAuditUser(_auditColumns.ReadUpdatedByColumn),
                UpdatedDtm = ReadAuditDate(_auditColumns.ReadUpdatedDtmColumn)
            };
        }
    }
}
