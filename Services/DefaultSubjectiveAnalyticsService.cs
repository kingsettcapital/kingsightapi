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
            IReadOnlyList<int> loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            DefaultSubjectiveAnalyticsBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);
    }

    public sealed class DefaultSubjectiveAnalyticsService : IDefaultSubjectiveAnalyticsService
    {
        private static readonly string[] MaturityDateColumnCandidates = ["maturity_date", "loan_maturity_date"];
        private static readonly string[] DefaultStatusColumnCandidates =
            ["default_subjective_status", "default_status_subjective"];
        private static readonly string[] ExitPlanColumnCandidates =
            ["subjective_exit_plan", "exit_plan", "default_exit_plan"];
        private static readonly string[] ExitDateColumnCandidates =
            ["subjective_exit_date", "exit_date", "default_exit_date"];
        private static readonly string[] MaturityDetailColumnCandidates =
            ["maturity_additional_detail", "maturity_detail", "maturity_addl_detail"];

        private readonly string _listSqlFrom;

        private readonly string _connectionString;
        private readonly FabricWarehouseTables _tables;
        private readonly string _tblDimLoan;
        private readonly string _tblLoanAliasMaster;
        private readonly string _tblDimStatus;
        private readonly ILogger<DefaultSubjectiveAnalyticsService> _logger;
        private string? _loanStatusKeyColumn;
        private string? _maturityDateColumn;
        private bool? _maturityDateColumnResolved;
        private string? _defaultStatusColumn;
        private bool? _defaultStatusColumnResolved;
        private string? _exitPlanColumn;
        private bool? _exitPlanColumnResolved;
        private string? _exitDateColumn;
        private bool? _exitDateColumnResolved;
        private string? _maturityDetailColumn;
        private bool? _maturityDetailColumnResolved;

        public DefaultSubjectiveAnalyticsService(
            IConfiguration configuration,
            ILogger<DefaultSubjectiveAnalyticsService> logger,
            FabricWarehouseTables tables)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _tables = tables;
            _tblDimLoan = tables.Mort("dim_loan");
            _tblLoanAliasMaster = tables.Mort("loan_alias_master");
            _tblDimStatus = tables.Mort("dim_status");

            _listSqlFrom = $"""
                from {_tblDimLoan} l
                left join {_tblLoanAliasMaster} m
                    on l.loan_alias_key = m.loan_alias_id
                where l.is_current = 1
                  and (l.is_leaf = 1 or l.is_leaf is null)
                """;
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
            IReadOnlyList<int> loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default)
        {
            await ResolveReadColumnsAsync(cancellationToken);

            var statusFilter = LoanStatusFilterParser.Parse(statuses);
            string? loanStatusKeyColumn = null;
            if (statusFilter.HasFilter)
            {
                loanStatusKeyColumn = await GetLoanStatusKeyColumnAsync(cancellationToken);
                if (string.IsNullOrEmpty(loanStatusKeyColumn))
                {
                    throw new InvalidOperationException("Status filter requires loan_status_key on mort.dim_loan.");
                }
            }

            var sql = BuildListSql(
                loanAliasIds,
                statusFilter,
                loanStatusKeyColumn,
                _maturityDateColumn,
                _defaultStatusColumn,
                _exitPlanColumn,
                _exitDateColumn,
                _maturityDetailColumn);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, loanAliasIds);
            LoanStatusFilterParser.AddParameters(command, statusFilter);

            var rows = new List<DefaultSubjectiveAnalyticsRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation(
                "Retrieved {Count} default subjective analytics rows for {AliasCount} loan alias filter(s).",
                rows.Count,
                loanAliasIds.Count);

            return rows;
        }

        public async Task<bool> UpdateAsync(
            DefaultSubjectiveAnalyticsBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await ResolveWritableColumnsAsync(cancellationToken);
            var missing = GetMissingWritableColumns();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "mort.dim_loan is missing subjective analytics columns: "
                    + string.Join(", ", missing)
                    + ". Run Scripts/Alter_dim_loan_default_subjective_analytics.sql.");
            }

            var setClause = string.Join(
                ", ",
                new[]
                {
                    $"{_defaultStatusColumn} = @default_subjective_status",
                    $"{_exitPlanColumn} = @subjective_exit_plan",
                    $"{_exitDateColumn} = @subjective_exit_date",
                    $"{_maturityDetailColumn} = @maturity_additional_detail",
                    "user_updated_by = @user_updated_by",
                    "user_updated_date = sysutcdatetime()"
                });

            var updateSql = $"""
                update {_tblDimLoan}
                set {setClause}
                where loan_key = @loan_key
                  and is_current = 1
                  and (is_leaf = 1 or is_leaf is null)
                """;

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

                await using var command = new SqlCommand(updateSql, connection);
                command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
                command.Parameters.AddWithValue(
                    "@default_subjective_status",
                    ToDbValue(DefaultSubjectiveAnalyticsValidation.CanonicalizeDefaultStatus(loan.ResolvedDefaultStatus)));
                command.Parameters.AddWithValue(
                    "@subjective_exit_plan",
                    ToDbValue(DefaultSubjectiveAnalyticsValidation.CanonicalizeExitPlan(loan.ResolvedExitPlan)));
                command.Parameters.AddWithValue(
                    "@subjective_exit_date",
                    ToDbValue(DefaultSubjectiveAnalyticsValidation.CanonicalizeExitDate(loan.ResolvedExitDate)));
                command.Parameters.AddWithValue(
                    "@maturity_additional_detail",
                    ToDbValue(string.IsNullOrWhiteSpace(loan.MaturityAdditionalDetail)
                        ? null
                        : loan.MaturityAdditionalDetail.Trim()));
                command.Parameters.AddWithValue("@user_updated_by", auditDisplayName);

                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);
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

        private async Task ResolveReadColumnsAsync(CancellationToken cancellationToken)
        {
            _ = await GetMaturityDateColumnAsync(cancellationToken);
            _ = await GetDefaultStatusColumnAsync(cancellationToken);
            _ = await GetExitPlanColumnAsync(cancellationToken);
            _ = await GetExitDateColumnAsync(cancellationToken);
            _ = await GetMaturityDetailColumnAsync(cancellationToken);
        }

        private Task ResolveWritableColumnsAsync(CancellationToken cancellationToken) =>
            ResolveReadColumnsAsync(cancellationToken);

        private IReadOnlyList<string> GetMissingWritableColumns()
        {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(_defaultStatusColumn))
            {
                missing.Add("default_subjective_status");
            }

            if (string.IsNullOrEmpty(_exitPlanColumn))
            {
                missing.Add("subjective_exit_plan");
            }

            if (string.IsNullOrEmpty(_exitDateColumn))
            {
                missing.Add("subjective_exit_date");
            }

            if (string.IsNullOrEmpty(_maturityDetailColumn))
            {
                missing.Add("maturity_additional_detail");
            }

            return missing;
        }

        private async Task<string?> GetMaturityDateColumnAsync(CancellationToken cancellationToken) =>
            await ResolveColumnAsync(
                MaturityDateColumnCandidates,
                "maturity date",
                () => _maturityDateColumn,
                v => _maturityDateColumn = v,
                () => _maturityDateColumnResolved,
                v => _maturityDateColumnResolved = v,
                cancellationToken);

        private async Task<string?> GetDefaultStatusColumnAsync(CancellationToken cancellationToken) =>
            await ResolveColumnAsync(
                DefaultStatusColumnCandidates,
                "default subjective status",
                () => _defaultStatusColumn,
                v => _defaultStatusColumn = v,
                () => _defaultStatusColumnResolved,
                v => _defaultStatusColumnResolved = v,
                cancellationToken);

        private async Task<string?> GetExitPlanColumnAsync(CancellationToken cancellationToken) =>
            await ResolveColumnAsync(
                ExitPlanColumnCandidates,
                "subjective exit plan",
                () => _exitPlanColumn,
                v => _exitPlanColumn = v,
                () => _exitPlanColumnResolved,
                v => _exitPlanColumnResolved = v,
                cancellationToken);

        private async Task<string?> GetExitDateColumnAsync(CancellationToken cancellationToken) =>
            await ResolveColumnAsync(
                ExitDateColumnCandidates,
                "subjective exit date",
                () => _exitDateColumn,
                v => _exitDateColumn = v,
                () => _exitDateColumnResolved,
                v => _exitDateColumnResolved = v,
                cancellationToken);

        private async Task<string?> GetMaturityDetailColumnAsync(CancellationToken cancellationToken) =>
            await ResolveColumnAsync(
                MaturityDetailColumnCandidates,
                "maturity additional detail",
                () => _maturityDetailColumn,
                v => _maturityDetailColumn = v,
                () => _maturityDetailColumnResolved,
                v => _maturityDetailColumnResolved = v,
                cancellationToken);

        private async Task<string?> ResolveColumnAsync(
            string[] candidates,
            string logLabel,
            Func<string?> getColumn,
            Action<string?> setColumn,
            Func<bool?> getResolved,
            Action<bool?> setResolved,
            CancellationToken cancellationToken)
        {
            if (getResolved() == true)
            {
                return getColumn();
            }

            var column = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _tblDimLoan,
                candidates,
                cancellationToken);

            setColumn(column);
            setResolved(true);

            if (!string.IsNullOrEmpty(column))
            {
                _logger.LogInformation(
                    "Using mort.dim_loan.{Column} for {Label}.",
                    column,
                    logLabel);
            }

            return column;
        }

        private async Task<string> GetLoanStatusKeyColumnAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_loanStatusKeyColumn))
            {
                return _loanStatusKeyColumn;
            }

            _loanStatusKeyColumn = await LoanDimStatusColumnResolver.ResolveAsync(
                _connectionString,
                _tblDimLoan,
                cancellationToken);

            return _loanStatusKeyColumn;
        }

        private string BuildListSql(
            IReadOnlyList<int> loanAliasIds,
            LoanStatusFilter statusFilter,
            string? loanStatusKeyColumn,
            string? maturityDateColumn,
            string? defaultStatusColumn,
            string? exitPlanColumn,
            string? exitDateColumn,
            string? maturityDetailColumn)
        {
            var sql = new StringBuilder();
            sql.AppendLine("""
                select l.loan_key,
                       l.loan_code,
                       l.loan_desc,
                       loan_alias_name = isnull(m.loan_alias_name, ''),
                """);
            sql.AppendLine($"       {SelectColumnOrNull(maturityDateColumn, "date", "maturity_date")},");
            sql.AppendLine($"       {SelectColumnOrNull(defaultStatusColumn, "varchar(50)", "default_subjective_status")},");
            sql.AppendLine($"       {SelectColumnOrNull(exitPlanColumn, "varchar(50)", "subjective_exit_plan")},");
            sql.AppendLine($"       {SelectColumnOrNull(exitDateColumn, "varchar(100)", "subjective_exit_date")},");
            sql.AppendLine($"       {SelectColumnOrNull(maturityDetailColumn, "varchar(500)", "maturity_additional_detail")},");
            sql.AppendLine("""
                       l.user_updated_by,
                       l.user_updated_date
                """);
            sql.Append(_listSqlFrom);

            sql.Append(" and l.loan_alias_key in (");
            sql.Append(string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}")));
            sql.Append(')');

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendSqlCondition(sql, "l", loanStatusKeyColumn, statusFilter, _tblDimStatus);
            }

            sql.AppendLine();
            sql.Append(" order by m.loan_alias_name, l.loan_code");
            return sql.ToString();
        }

        private static string SelectColumnOrNull(string? column, string sqlType, string alias) =>
            string.IsNullOrEmpty(column)
                ? $"cast(null as {sqlType}) as {alias}"
                : $"l.{column} as {alias}";

        private static void AddLoanAliasParameters(SqlCommand command, IReadOnlyList<int> loanAliasIds)
        {
            for (var i = 0; i < loanAliasIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@loan_alias_id_{i}", loanAliasIds[i]);
            }
        }

        private static DefaultSubjectiveAnalyticsRowDto MapRow(SqlDataReader reader) =>
            new()
            {
                LoanKey = GetInt64(reader, "loan_key"),
                LoanId = GetString(reader, "loan_code"),
                Description = GetString(reader, "loan_desc"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                MaturityDate = GetNullableDate(reader, "maturity_date"),
                DefaultStatus = GetNullableString(reader, "default_subjective_status"),
                ExitPlan = GetNullableString(reader, "subjective_exit_plan"),
                ExitDate = GetNullableString(reader, "subjective_exit_date"),
                MaturityAdditionalDetail = GetNullableString(reader, "maturity_additional_detail"),
                UserUpdatedBy = GetNullableString(reader, "user_updated_by"),
                UserUpdatedDate = GetNullableDateTime(reader, "user_updated_date")
            };

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

        private static DateTime? GetNullableDateTime(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
    }
}
