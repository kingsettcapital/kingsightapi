using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IDefaultDateCaptureService
    {
        Task<IReadOnlyList<DefaultDateCaptureRowDto>> GetAsync(
            IReadOnlyList<int>? loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            DefaultDateCaptureBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);
    }

    public sealed class DefaultDateCaptureService : IDefaultDateCaptureService
    {
        private readonly string _connectionString;
        private readonly SubjectiveInputSql _sql;
        private readonly INotificationService _notificationService;
        private readonly ILogger<DefaultDateCaptureService> _logger;

        private bool _schemaProbed;
        private SubjectiveInputRelationshipAuditColumns _auditColumns = new();
        private string? _loanStatusKeyColumn;

        public DefaultDateCaptureService(
            IConfiguration configuration,
            ILogger<DefaultDateCaptureService> logger,
            FabricWarehouseTables tables,
            INotificationService notificationService)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _notificationService = notificationService;
            _sql = new SubjectiveInputSql(tables);
        }

        public async Task<IReadOnlyList<DefaultDateCaptureRowDto>> GetAsync(
            IReadOnlyList<int>? loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            var statusFilter = LoanStatusFilterParser.Parse(statuses);
            string? loanStatusKeyColumn = null;
            if (statusFilter.HasFilter)
            {
                loanStatusKeyColumn = await TryResolveLoanStatusKeyColumnAsync(cancellationToken);
            }

            var sql = BuildListSql(loanAliasIds, statusFilter, loanStatusKeyColumn);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            if (loanAliasIds is { Count: > 0 })
            {
                AddLoanAliasParameters(command, loanAliasIds);
            }

            LoanStatusFilterParser.AddParameters(command, statusFilter);

            try
            {
                return await ReadRowsAsync(command, loanAliasIds, cancellationToken);
            }
            catch (SqlException ex) when (statusFilter.HasFilter)
            {
                _logger.LogWarning(
                    ex,
                    "Default date capture query failed with status filter; retrying without status filter.");
                return await GetAsync(loanAliasIds, null, cancellationToken);
            }
        }

        private async Task<IReadOnlyList<DefaultDateCaptureRowDto>> ReadRowsAsync(
            SqlCommand command,
            IReadOnlyList<int>? loanAliasIds,
            CancellationToken cancellationToken)
        {
            var rows = new List<DefaultDateCaptureRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation(
                "Retrieved {Count} default date capture rows (aliasFilter={AliasCount}).",
                rows.Count,
                loanAliasIds?.Count ?? 0);

            return rows;
        }

        public async Task<bool> UpdateAsync(
            DefaultDateCaptureBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                DateTime? priorDefaultDate = await TryGetPriorDefaultDateAsync(connection, loan, cancellationToken);

                var rowsChanged = loan.LoanKey > 0
                    ? await ExecuteUpdateAsync(
                        BuildUpdateByLoanKeySql(),
                        loan,
                        auditDisplayName,
                        connection,
                        cancellationToken)
                    : 0;

                if (rowsChanged == 0 && !string.IsNullOrWhiteSpace(loan.LoanCode))
                {
                    rowsChanged = await ExecuteUpdateAsync(
                        BuildUpdateByLoanCodeSql(),
                        loan,
                        auditDisplayName,
                        connection,
                        cancellationToken);
                }

                if (rowsChanged > 0)
                {
                    await _notificationService.CreateDefaultDateUpdateAsync(
                        loan.LoanCode,
                        priorDefaultDate,
                        loan.DefaultDate,
                        auditDisplayName,
                        cancellationToken);
                }

                affectedRows += rowsChanged;
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Updated {AffectedRows} default date capture loan rows.", affectedRows);
                return true;
            }

            _logger.LogWarning("No default date capture loan rows updated.");
            return false;
        }

        private async Task<int> ExecuteUpdateAsync(
            string sql,
            DefaultDateCaptureUpdateItem loan,
            string auditDisplayName,
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
            command.Parameters.AddWithValue("@loan_code", loan.LoanCode?.Trim() ?? string.Empty);
            command.Parameters.AddWithValue(
                "@default_date",
                loan.DefaultDate.HasValue ? loan.DefaultDate.Value.Date : DBNull.Value);
            _auditColumns.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<DateTime?> TryGetPriorDefaultDateAsync(
            SqlConnection connection,
            DefaultDateCaptureUpdateItem loan,
            CancellationToken cancellationToken)
        {
            var sql = loan.LoanKey > 0
                ? $"""
                  select top 1 r.default_date
                  from {_sql.LoanAliasRelationship} r
                  inner join {_sql.SharedDimLoan} l on l.loan_key = @loan_key and l.loan_code = r.loan_code
                  """
                : $"""
                  select top 1 r.default_date
                  from {_sql.LoanAliasRelationship} r
                  where r.loan_code = @loan_code
                  """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
            command.Parameters.AddWithValue("@loan_code", loan.LoanCode?.Trim() ?? string.Empty);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull)
            {
                return null;
            }

            return Convert.ToDateTime(result).Date;
        }

        private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            if (_schemaProbed)
            {
                return;
            }

            _auditColumns = await SubjectiveInputRelationshipAuditColumns.ProbeAsync(
                _connectionString,
                _sql.LoanAliasRelationship,
                cancellationToken);
            _schemaProbed = true;
        }

        private string BuildListSql(
            IReadOnlyList<int>? loanAliasIds,
            LoanStatusFilter statusFilter,
            string? loanStatusKeyColumn)
        {
            var needsStatusJoin = statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn);

            var sql = new StringBuilder(
                $"""
                 select loan_key = cast(0 as bigint),
                        r.loan_code,
                        r.loan_description,
                        r.loan_alias_name,
                        r.loan_term_default_date,
                        r.default_date,
                        user_updated_by = {_auditColumns.BuildSelectUpdatedByExpression()},
                        user_updated_date = {_auditColumns.BuildSelectUpdatedDtmExpression()}
                 from {_sql.LoanAliasRelationship} r
                 """);

            if (loanAliasIds is { Count: > 0 })
            {
                sql.AppendLine(
                    $"""
                     inner join {_sql.LoanAliasMaster} m
                         on r.loan_alias_name = m.loan_alias_name
                     """);
            }

            if (needsStatusJoin)
            {
                sql.AppendLine(_sql.SharedDimLoanJoinOnLoanCode());
            }

            if (loanAliasIds is { Count: > 0 })
            {
                sql.Append(" where m.loan_alias_id in (");
                sql.Append(string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}")));
                sql.Append(')');

                if (needsStatusJoin)
                {
                    LoanStatusFilterParser.AppendSqlCondition(
                        sql,
                        "l",
                        loanStatusKeyColumn!,
                        statusFilter,
                        _sql.DimStatus);
                }
            }
            else if (needsStatusJoin)
            {
                sql.AppendLine(" where 1 = 1");
                LoanStatusFilterParser.AppendSqlCondition(
                    sql,
                    "l",
                    loanStatusKeyColumn!,
                    statusFilter,
                    _sql.DimStatus);
            }

            sql.AppendLine();
            sql.Append(" order by r.loan_alias_name, r.loan_code");
            return sql.ToString();
        }

        private string BuildUpdateByLoanKeySql() =>
            $"""
                update r
                set default_date = @default_date{_auditColumns.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
                inner join {_sql.SharedDimLoan} l
                    on l.loan_key = @loan_key
                   and {SubjectiveInputSql.EqualsVarchar("l", "loan_code", "r", "loan_code")}
                   and {SubjectiveInputSql.DimLoanIsCurrent("l")}
                """;

        private string BuildUpdateByLoanCodeSql() =>
            $"""
                update r
                set default_date = @default_date{_auditColumns.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
                where cast(r.loan_code as varchar(100)) collate database_default = cast(@loan_code as varchar(100)) collate database_default
                """;

        private async Task<string?> TryResolveLoanStatusKeyColumnAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_loanStatusKeyColumn))
            {
                return _loanStatusKeyColumn;
            }

            try
            {
                _loanStatusKeyColumn = await LoanDimStatusColumnResolver.ResolveAsync(
                    _connectionString,
                    _sql.SharedDimLoan,
                    cancellationToken);
                return _loanStatusKeyColumn;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Default date capture status filter skipped; shared.dim_loan status column unavailable.");
                return null;
            }
        }

        private static void AddLoanAliasParameters(SqlCommand command, IReadOnlyList<int> loanAliasIds)
        {
            for (var i = 0; i < loanAliasIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@loan_alias_id_{i}", loanAliasIds[i]);
            }
        }

        private static DefaultDateCaptureRowDto MapRow(SqlDataReader reader)
        {
            DateTime? updatedDate = null;
            if (TryGetOrdinal(reader, "user_updated_date", out var updatedOrdinal) && !reader.IsDBNull(updatedOrdinal))
            {
                updatedDate = DateTime.SpecifyKind(reader.GetDateTime(updatedOrdinal), DateTimeKind.Utc);
            }

            return new DefaultDateCaptureRowDto
            {
                LoanKey = GetInt64(reader, "loan_key"),
                LoanId = GetString(reader, "loan_code"),
                Description = GetString(reader, "loan_description"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                LoanTermDefaultDate = GetNullableDate(reader, "loan_term_default_date"),
                DefaultDate = GetNullableDate(reader, "default_date"),
                UserUpdatedBy = TryGetOrdinal(reader, "user_updated_by", out var byOrdinal) && !reader.IsDBNull(byOrdinal)
                    ? reader.GetString(byOrdinal)
                    : string.Empty,
                UserUpdatedDate = updatedDate
            };
        }

        private static bool TryGetOrdinal(SqlDataReader reader, string name, out int ordinal)
        {
            try
            {
                ordinal = reader.GetOrdinal(name);
                return true;
            }
            catch (IndexOutOfRangeException)
            {
                ordinal = -1;
                return false;
            }
        }

        private static long GetInt64(SqlDataReader reader, string name) =>
            reader.IsDBNull(reader.GetOrdinal(name))
                ? 0L
                : Convert.ToInt64(reader.GetValue(reader.GetOrdinal(name)));

        private static string GetString(SqlDataReader reader, string name) =>
            reader.IsDBNull(reader.GetOrdinal(name))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal(name));

        private static DateTime? GetNullableDate(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetFieldType(ordinal) == typeof(DateTime)
                ? reader.GetDateTime(ordinal).Date
                : Convert.ToDateTime(reader.GetValue(ordinal)).Date;
        }
    }
}
