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
            IReadOnlyList<int> loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            DefaultDateCaptureBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);
    }

    public sealed class DefaultDateCaptureService : IDefaultDateCaptureService
    {
        private static readonly string[] DefaultDateColumnCandidates =
        [
            "default_date",
            "loan_default_date"
        ];

        private static readonly string[] LoanTermDefaultDateColumnCandidates =
        [
            "loan_term_default_date",
            "term_default_date",
            "default_date_per_loan_terms"
        ];

        private readonly string _listSqlFrom;

        private readonly string _connectionString;
        private readonly FabricWarehouseTables _tables;
        private readonly string _tblDimLoan;
        private readonly string _tblLoanAliasMaster;
        private readonly string _tblDimStatus;
        private readonly ILogger<DefaultDateCaptureService> _logger;
        private string? _loanStatusKeyColumn;
        private string? _defaultDateColumn;
        private bool? _defaultDateColumnResolved;
        private string? _loanTermDefaultDateColumn;
        private bool? _loanTermDefaultDateColumnResolved;

        public DefaultDateCaptureService(
            IConfiguration configuration,
            ILogger<DefaultDateCaptureService> logger,
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

        public async Task<IReadOnlyList<DefaultDateCaptureRowDto>> GetAsync(
            IReadOnlyList<int> loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default)
        {
            var defaultDateColumn = await GetDefaultDateColumnAsync(cancellationToken);
            var loanTermDefaultDateColumn = await GetLoanTermDefaultDateColumnAsync(cancellationToken);

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
                defaultDateColumn,
                loanTermDefaultDateColumn);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, loanAliasIds);

            LoanStatusFilterParser.AddParameters(command, statusFilter);

            var rows = new List<DefaultDateCaptureRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation(
                "Retrieved {Count} default date capture rows for {AliasCount} loan alias filter(s).",
                rows.Count,
                loanAliasIds.Count);

            return rows;
        }

        public async Task<bool> UpdateAsync(
            DefaultDateCaptureBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            var defaultDateColumn = await GetDefaultDateColumnAsync(cancellationToken);
            if (string.IsNullOrEmpty(defaultDateColumn))
            {
                throw new InvalidOperationException(
                    "mort.dim_loan is missing default_date. Run DDL to add the column before saving.");
            }

            var updateSql = $"""
                update {_tblDimLoan}
                set {defaultDateColumn} = @default_date,
                    user_updated_by = @user_updated_by,
                    user_updated_date = sysutcdatetime()
                where loan_key = @loan_key
                  and is_current = 1
                  and (is_leaf = 1 or is_leaf is null)
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                await using var command = new SqlCommand(updateSql, connection);
                command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
                command.Parameters.AddWithValue(
                    "@default_date",
                    loan.DefaultDate.HasValue ? loan.DefaultDate.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@user_updated_by", auditDisplayName);

                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Updated {AffectedRows} default date capture loan rows.", affectedRows);
                return true;
            }

            _logger.LogWarning("No default date capture loan rows updated.");
            return false;
        }

        private string BuildListSql(
            IReadOnlyList<int> loanAliasIds,
            LoanStatusFilter statusFilter,
            string? loanStatusKeyColumn,
            string? defaultDateColumn,
            string? loanTermDefaultDateColumn)
        {
            var defaultDateSelect = string.IsNullOrEmpty(defaultDateColumn)
                ? "cast(null as date) as default_date"
                : $"l.{defaultDateColumn} as default_date";

            var loanTermDefaultDateSelect = string.IsNullOrEmpty(loanTermDefaultDateColumn)
                ? "cast(null as date) as loan_term_default_date"
                : $"l.{loanTermDefaultDateColumn} as loan_term_default_date";

            var sql = new StringBuilder();
            sql.AppendLine("""
                select l.loan_key,
                       l.loan_code,
                       l.loan_desc,
                       loan_alias_name = isnull(m.loan_alias_name, ''),
                """);
            sql.AppendLine($"       {loanTermDefaultDateSelect},");
            sql.AppendLine($"       {defaultDateSelect},");
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

        private async Task<string?> GetDefaultDateColumnAsync(CancellationToken cancellationToken)
        {
            if (_defaultDateColumnResolved == true)
            {
                return _defaultDateColumn;
            }

            _defaultDateColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _tblDimLoan,
                DefaultDateColumnCandidates,
                cancellationToken);

            _defaultDateColumnResolved = true;
            if (!string.IsNullOrEmpty(_defaultDateColumn))
            {
                _logger.LogInformation(
                    "Using mort.dim_loan.{Column} for default date capture.",
                    _defaultDateColumn);
            }

            return _defaultDateColumn;
        }

        private async Task<string?> GetLoanTermDefaultDateColumnAsync(CancellationToken cancellationToken)
        {
            if (_loanTermDefaultDateColumnResolved == true)
            {
                return _loanTermDefaultDateColumn;
            }

            _loanTermDefaultDateColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _tblDimLoan,
                LoanTermDefaultDateColumnCandidates,
                cancellationToken);

            _loanTermDefaultDateColumnResolved = true;
            if (!string.IsNullOrEmpty(_loanTermDefaultDateColumn))
            {
                _logger.LogInformation(
                    "Using mort.dim_loan.{Column} for loan term default date.",
                    _loanTermDefaultDateColumn);
            }

            return _loanTermDefaultDateColumn;
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

        private static void AddLoanAliasParameters(SqlCommand command, IReadOnlyList<int> loanAliasIds)
        {
            for (var i = 0; i < loanAliasIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@loan_alias_id_{i}", loanAliasIds[i]);
            }
        }

        private static DefaultDateCaptureRowDto MapRow(SqlDataReader reader)
        {
            return new DefaultDateCaptureRowDto
            {
                LoanKey = GetInt64(reader, "loan_key"),
                LoanId = GetString(reader, "loan_code"),
                Description = GetString(reader, "loan_desc"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                LoanTermDefaultDate = GetNullableDate(reader, "loan_term_default_date"),
                DefaultDate = GetNullableDate(reader, "default_date"),
                UserUpdatedBy = GetNullableString(reader, "user_updated_by"),
                UserUpdatedDate = GetNullableDateTime(reader, "user_updated_date")
            };
        }

        private static long GetInt64(SqlDataReader reader, string name) =>
            reader.IsDBNull(reader.GetOrdinal(name))
                ? 0L
                : Convert.ToInt64(reader.GetValue(reader.GetOrdinal(name)));

        private static string GetString(SqlDataReader reader, string name) =>
            reader.IsDBNull(reader.GetOrdinal(name))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal(name));

        private static string? GetNullableString(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
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
