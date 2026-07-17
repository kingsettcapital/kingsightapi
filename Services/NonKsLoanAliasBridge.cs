using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    /// <summary>
    /// Keeps Non-KS <c>external_serviced_loan</c> codes in sync with
    /// <c>loan_alias_relationship</c> so they appear on Loan Alias Assignment
    /// and assigned aliases flow back to Non-KS grids.
    /// </summary>
    public interface INonKsLoanAliasBridge
    {
        Task EnsureMissingRelationshipRowsAsync(
            SqlConnection connection,
            CancellationToken cancellationToken = default);

        Task EnsureRelationshipRowAsync(
            SqlConnection connection,
            string loanCode,
            string? loanDescription,
            string? loanAliasName,
            string auditDisplayName,
            CancellationToken cancellationToken = default);

        Task SyncAliasToExternalServicedLoanAsync(
            SqlConnection connection,
            string loanCode,
            string? loanAliasName,
            CancellationToken cancellationToken = default);

        Task<int> CascadeAliasRenameOnExternalServicedLoanAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string previousName,
            string newName,
            CancellationToken cancellationToken = default);
    }

    public sealed class NonKsLoanAliasBridge : INonKsLoanAliasBridge
    {
        private readonly string _connectionString;
        private readonly SubjectiveInputSql _sql;
        private readonly ILogger<NonKsLoanAliasBridge> _logger;

        private bool _schemaProbed;
        private SubjectiveInputRelationshipAuditColumns _relationshipAudit = new();
        private string? _eslAliasColumn;
        private string? _eslDescriptionColumn;
        private string? _eslExtLoanCodeColumn;

        public NonKsLoanAliasBridge(
            IConfiguration configuration,
            FabricWarehouseTables tables,
            ILogger<NonKsLoanAliasBridge> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _sql = new SubjectiveInputSql(tables);
            _logger = logger;
        }

        public async Task EnsureMissingRelationshipRowsAsync(
            SqlConnection connection,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);
            if (_eslExtLoanCodeColumn is null)
            {
                return;
            }

            var missing = await ListMissingNonKsCodesAsync(connection, cancellationToken);
            if (missing.Count == 0)
            {
                return;
            }

            foreach (var row in missing)
            {
                await InsertRelationshipRowIfMissingAsync(
                    connection,
                    null,
                    row.LoanCode,
                    row.Description,
                    row.AliasName,
                    "system",
                    cancellationToken);
            }

            _logger.LogInformation(
                "Synced {Count} Non-KS loan(s) into loan_alias_relationship for Assignment.",
                missing.Count);
        }

        public async Task EnsureRelationshipRowAsync(
            SqlConnection connection,
            string loanCode,
            string? loanDescription,
            string? loanAliasName,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);
            var code = loanCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            await InsertRelationshipRowIfMissingAsync(
                connection,
                null,
                code,
                loanDescription,
                loanAliasName,
                string.IsNullOrWhiteSpace(auditDisplayName) ? "system" : auditDisplayName.Trim(),
                cancellationToken);
        }

        public async Task SyncAliasToExternalServicedLoanAsync(
            SqlConnection connection,
            string loanCode,
            string? loanAliasName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);
            if (_eslExtLoanCodeColumn is null || _eslAliasColumn is null)
            {
                return;
            }

            var code = loanCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            var sql = $"""
                update {_sql.ExternalServicedLoan}
                set [{_eslAliasColumn}] = @loan_alias_name
                where cast([{_eslExtLoanCodeColumn}] as varchar(100)) collate database_default
                    = cast(@loan_code as varchar(100)) collate database_default
                """;

            await using var command = new SqlCommand(sql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@loan_code", code);
            command.Parameters.AddWithValue(
                "@loan_alias_name",
                string.IsNullOrWhiteSpace(loanAliasName) ? DBNull.Value : loanAliasName.Trim());

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected > 0)
            {
                _logger.LogInformation(
                    "Synced loan alias to {Count} Non-KS external_serviced_loan row(s) for {LoanCode}.",
                    affected,
                    code);
            }
        }

        public async Task<int> CascadeAliasRenameOnExternalServicedLoanAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string previousName,
            string newName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);
            if (_eslAliasColumn is null)
            {
                return 0;
            }

            var sql = $"""
                update {_sql.ExternalServicedLoan}
                set [{_eslAliasColumn}] = @new_name
                where cast([{_eslAliasColumn}] as varchar(200)) collate database_default
                    = cast(@old_name as varchar(200)) collate database_default
                """;

            await using var command = transaction is null
                ? new SqlCommand(sql, connection)
                : new SqlCommand(sql, connection, transaction);
            command.CommandType = System.Data.CommandType.Text;
            command.Parameters.AddWithValue("@new_name", newName);
            command.Parameters.AddWithValue("@old_name", previousName);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            if (_schemaProbed)
            {
                return;
            }

            _relationshipAudit = await SubjectiveInputRelationshipAuditColumns.ProbeAsync(
                _connectionString,
                _sql.LoanAliasRelationship,
                cancellationToken);

            _eslExtLoanCodeColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.ExternalServicedLoan,
                ["ext_loan_code"],
                cancellationToken);
            _eslAliasColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.ExternalServicedLoan,
                ["loan_alias_name"],
                cancellationToken);
            _eslDescriptionColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.ExternalServicedLoan,
                ["description", "loan_description", "loan_name"],
                cancellationToken);

            _schemaProbed = true;
        }

        private async Task<IReadOnlyList<MissingNonKsLoan>> ListMissingNonKsCodesAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            var descriptionExpr = _eslDescriptionColumn is null
                ? "cast('' as varchar(500))"
                : $"isnull(max(cast(e.[{_eslDescriptionColumn}] as varchar(500))), '')";
            var aliasExpr = _eslAliasColumn is null
                ? "cast(null as varchar(200))"
                : $"max(cast(e.[{_eslAliasColumn}] as varchar(200)))";

            var sql = $"""
                select
                    loan_code = cast(e.[{_eslExtLoanCodeColumn}] as varchar(100)),
                    loan_description = {descriptionExpr},
                    loan_alias_name = {aliasExpr}
                from {_sql.ExternalServicedLoan} e
                where e.[{_eslExtLoanCodeColumn}] is not null
                  and ltrim(rtrim(cast(e.[{_eslExtLoanCodeColumn}] as varchar(100)))) <> ''
                  and not exists (
                      select 1
                      from {_sql.LoanAliasRelationship} r
                      where cast(r.loan_code as varchar(100)) collate database_default
                          = cast(e.[{_eslExtLoanCodeColumn}] as varchar(100)) collate database_default
                  )
                group by cast(e.[{_eslExtLoanCodeColumn}] as varchar(100))
                """;

            var rows = new List<MissingNonKsLoan>();
            await using var command = new SqlCommand(sql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0));
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                rows.Add(new MissingNonKsLoan(
                    code.Trim(),
                    reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1)),
                    reader.IsDBNull(2) ? null : Convert.ToString(reader.GetValue(2))));
            }

            return rows;
        }

        private async Task InsertRelationshipRowIfMissingAsync(
            SqlConnection connection,
            SqlTransaction? transaction,
            string loanCode,
            string? loanDescription,
            string? loanAliasName,
            string auditDisplayName,
            CancellationToken cancellationToken)
        {
            var existsSql = $"""
                select top 1 1
                from {_sql.LoanAliasRelationship}
                where cast(loan_code as varchar(100)) collate database_default
                    = cast(@loan_code as varchar(100)) collate database_default
                """;

            await using (var existsCommand = transaction is null
                ? new SqlCommand(existsSql, connection)
                : new SqlCommand(existsSql, connection, transaction))
            {
                existsCommand.Parameters.AddWithValue("@loan_code", loanCode);
                var exists = await existsCommand.ExecuteScalarAsync(cancellationToken);
                if (exists is not null && exists != DBNull.Value)
                {
                    return;
                }
            }

            var (auditColumns, auditValues) = _relationshipAudit.BuildInsertColumnList();
            var columns = new List<string> { "loan_code", "loan_description", "loan_alias_name" };
            var values = new List<string> { "@loan_code", "@loan_description", "@loan_alias_name" };
            columns.AddRange(auditColumns);
            values.AddRange(auditValues);

            var insertSql = $"""
                insert into {_sql.LoanAliasRelationship} (
                    {string.Join(", ", columns)})
                values (
                    {string.Join(", ", values)})
                """;

            await using var insertCommand = transaction is null
                ? new SqlCommand(insertSql, connection)
                : new SqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("@loan_code", loanCode);
            insertCommand.Parameters.AddWithValue(
                "@loan_description",
                string.IsNullOrWhiteSpace(loanDescription) ? string.Empty : loanDescription.Trim());
            insertCommand.Parameters.AddWithValue(
                "@loan_alias_name",
                string.IsNullOrWhiteSpace(loanAliasName) ? DBNull.Value : loanAliasName.Trim());
            _relationshipAudit.AddUpdateParameters(insertCommand, auditDisplayName, DateTime.UtcNow);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        private sealed record MissingNonKsLoan(string LoanCode, string? Description, string? AliasName);
    }
}
