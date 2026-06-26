using System.Data;
using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IDefaultSubjectiveAnalyticsService
    {
        IReadOnlyList<DefaultSubjectiveAnalyticsOptionDto> GetDefaultStatusOptions();
        IReadOnlyList<DefaultSubjectiveAnalyticsOptionDto> GetExitPlanOptions();
        DefaultSubjectiveAnalyticsLookupsDto GetLookups();

        Task<IReadOnlyList<DefaultSubjectiveAnalyticsRowDto>> GetAsync(
            IReadOnlyList<int>? loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            DefaultSubjectiveAnalyticsBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);
    }

    public sealed class DefaultSubjectiveAnalyticsService : IDefaultSubjectiveAnalyticsService
    {
        private readonly string _connectionString;
        private readonly SubjectiveInputSql _sql;
        private readonly ILogger<DefaultSubjectiveAnalyticsService> _logger;

        private bool _schemaProbed;
        private SubjectiveInputRelationshipAuditColumns _auditColumns = new();
        private string? _loanStatusKeyColumn;
        private bool _exitDateIsTextColumn = true;

        public DefaultSubjectiveAnalyticsService(
            IConfiguration configuration,
            ILogger<DefaultSubjectiveAnalyticsService> logger,
            FabricWarehouseTables tables)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _sql = new SubjectiveInputSql(tables);
        }

        public IReadOnlyList<DefaultSubjectiveAnalyticsOptionDto> GetDefaultStatusOptions() =>
            DefaultSubjectiveAnalyticsValidation.ToOptions(
                DefaultSubjectiveAnalyticsTokens.DefaultStatusOptions);

        public IReadOnlyList<DefaultSubjectiveAnalyticsOptionDto> GetExitPlanOptions() =>
            DefaultSubjectiveAnalyticsValidation.ToOptions(
                DefaultSubjectiveAnalyticsTokens.ExitPlanOptions);

        public DefaultSubjectiveAnalyticsLookupsDto GetLookups()
        {
            var defaultStatusOptions = GetDefaultStatusOptions();
            var exitPlanOptions = GetExitPlanOptions();
            return new DefaultSubjectiveAnalyticsLookupsDto
            {
                DefaultStatusOptions = defaultStatusOptions,
                ExitPlanOptions = exitPlanOptions,
                DefaultStatuses = defaultStatusOptions.Select(o => o.Value).ToList(),
                ExitPlans = exitPlanOptions.Select(o => o.Value).ToList()
            };
        }

        public async Task<IReadOnlyList<DefaultSubjectiveAnalyticsRowDto>> GetAsync(
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
                    "Default subjective analytics query failed with status filter; retrying without status filter.");
                return await GetAsync(loanAliasIds, null, cancellationToken);
            }
        }

        public async Task<bool> UpdateAsync(
            DefaultSubjectiveAnalyticsBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                var validationError = DefaultSubjectiveAnalyticsValidation.ValidateUpdateItem(loan);
                if (validationError is not null)
                {
                    throw new InvalidOperationException(validationError);
                }

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

                affectedRows += rowsChanged;
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Updated {AffectedRows} default subjective analytics loan rows.",
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No default subjective analytics loan rows updated.");
            return false;
        }

        private async Task<IReadOnlyList<DefaultSubjectiveAnalyticsRowDto>> ReadRowsAsync(
            SqlCommand command,
            IReadOnlyList<int>? loanAliasIds,
            CancellationToken cancellationToken)
        {
            var rows = new List<DefaultSubjectiveAnalyticsRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation(
                "Retrieved {Count} default subjective analytics rows (aliasFilter={AliasCount}).",
                rows.Count,
                loanAliasIds?.Count ?? 0);

            return rows;
        }

        private async Task<int> ExecuteUpdateAsync(
            string sql,
            DefaultSubjectiveAnalyticsUpdateItem loan,
            string auditDisplayName,
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
            command.Parameters.AddWithValue("@loan_code", loan.LoanCode?.Trim() ?? string.Empty);
            AddTextParameter(
                command,
                "@default_subjective_status",
                DefaultSubjectiveAnalyticsValidation.CanonicalizeDefaultStatus(loan.ResolvedDefaultStatus));
            AddTextParameter(
                command,
                "@subjective_exit_plan",
                DefaultSubjectiveAnalyticsValidation.CanonicalizeExitPlan(loan.ResolvedExitPlan));
            AddTextParameter(
                command,
                "@subjective_exit_date",
                DefaultSubjectiveAnalyticsValidation.CanonicalizeExitDate(loan.ResolvedExitDate));
            AddTextParameter(
                command,
                "@maturity_additional_detail",
                string.IsNullOrWhiteSpace(loan.MaturityAdditionalDetail)
                    ? null
                    : loan.MaturityAdditionalDetail.Trim());
            _auditColumns.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static void AddTextParameter(SqlCommand command, string name, string? value)
        {
            var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, 500);
            parameter.Value = string.IsNullOrEmpty(value) ? DBNull.Value : value;
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
            _exitDateIsTextColumn = await IsTextColumnAsync(
                _sql.LoanAliasRelationship,
                "exit_date",
                cancellationToken);
            _schemaProbed = true;
        }

        private async Task<bool> IsTextColumnAsync(
            string tableName,
            string columnName,
            CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand($"select top (0) [{columnName}] from {tableName}", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var dataType = reader.GetDataTypeName(reader.GetOrdinal(columnName));

            return dataType.Contains("char", StringComparison.OrdinalIgnoreCase)
                || dataType.Equals("text", StringComparison.OrdinalIgnoreCase)
                || dataType.Equals("ntext", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildExitDateSetClause() =>
            _exitDateIsTextColumn
                ? "exit_date = @subjective_exit_date"
                : "exit_date = try_convert(date, @subjective_exit_date, 103)";

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
                        r.maturity_date,
                        r.default_status,
                        r.exit_plan,
                        r.exit_date,
                        r.maturity_notes,
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
                set default_status = @default_subjective_status,
                    exit_plan = @subjective_exit_plan,
                    {BuildExitDateSetClause()},
                    maturity_notes = @maturity_additional_detail{_auditColumns.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
                inner join {_sql.SharedDimLoan} l
                    on l.loan_key = @loan_key
                   and {SubjectiveInputSql.EqualsVarchar("l", "loan_code", "r", "loan_code")}
                   and {SubjectiveInputSql.DimLoanIsCurrent("l")}
                """;

        private string BuildUpdateByLoanCodeSql() =>
            $"""
                update r
                set default_status = @default_subjective_status,
                    exit_plan = @subjective_exit_plan,
                    {BuildExitDateSetClause()},
                    maturity_notes = @maturity_additional_detail{_auditColumns.BuildUpdateSetClause()}
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
                _logger.LogWarning(
                    ex,
                    "Default subjective analytics status filter skipped; shared.dim_loan status column unavailable.");
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

        private static DefaultSubjectiveAnalyticsRowDto MapRow(SqlDataReader reader)
        {
            DateTime? updatedDate = null;
            if (reader.TryGetOrdinal("user_updated_date", out var dateOrd) && !reader.IsDBNull(dateOrd))
            {
                updatedDate = DateTime.SpecifyKind(reader.GetDateTime(dateOrd), DateTimeKind.Utc);
            }

            return new DefaultSubjectiveAnalyticsRowDto
            {
                LoanKey = GetInt64(reader, "loan_key"),
                LoanId = GetString(reader, "loan_code"),
                Description = GetString(reader, "loan_description"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                MaturityDate = GetNullableDate(reader, "maturity_date"),
                DefaultStatus = GetNullableString(reader, "default_status"),
                ExitPlan = GetNullableString(reader, "exit_plan"),
                ExitDate = GetNullableString(reader, "exit_date"),
                MaturityAdditionalDetail = GetNullableString(reader, "maturity_notes"),
                UserUpdatedBy = reader.TryGetOrdinal("user_updated_by", out var byOrd) && !reader.IsDBNull(byOrd)
                    ? reader.GetString(byOrd)
                    : null,
                UserUpdatedDate = updatedDate
            };
        }

        private static object ToDbValue(string? value) =>
            string.IsNullOrEmpty(value) ? DBNull.Value : value;

        private static long GetInt64(SqlDataReader reader, string name) =>
            reader.IsDBNull(reader.GetOrdinal(name))
                ? 0L
                : Convert.ToInt64(reader.GetValue(reader.GetOrdinal(name)));

        private static string GetString(SqlDataReader reader, string name) =>
            reader.IsDBNull(reader.GetOrdinal(name))
                ? string.Empty
                : Convert.ToString(reader.GetValue(reader.GetOrdinal(name))) ?? string.Empty;

        private static string? GetNullableString(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var text = Convert.ToString(reader.GetValue(ordinal));
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

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
