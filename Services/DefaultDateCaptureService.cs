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
        private readonly string _listSqlFrom;
        private readonly string _updateSql;

        private readonly string _connectionString;
        private readonly FabricWarehouseTables _tables;
        private readonly string _tblDimLoan;
        private readonly string _tblLoanAliasMaster;
        private readonly string _tblLoanAliasRelationship;
        private readonly string _tblDimStatus;
        private readonly ILogger<DefaultDateCaptureService> _logger;
        private string? _loanStatusKeyColumn;

        public DefaultDateCaptureService(
            IConfiguration configuration,
            ILogger<DefaultDateCaptureService> logger,
            FabricWarehouseTables tables)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _tables = tables;
            var subjective = new SubjectiveInputSql(tables);
            _tblDimLoan = subjective.SharedDimLoan;
            _tblLoanAliasMaster = subjective.LoanAliasMaster;
            _tblLoanAliasRelationship = subjective.LoanAliasRelationship;
            _tblDimStatus = subjective.DimStatus;

            _listSqlFrom = $"""
                from {_tblLoanAliasRelationship} r
                inner join {_tblLoanAliasMaster} m
                    on r.loan_alias_name = m.loan_alias_name
                {subjective.SharedDimLoanJoinOnLoanCode()}
                """;

            _updateSql = $"""
                update r
                set default_date = @default_date
                from {_tblLoanAliasRelationship} r
                inner join {_tblDimLoan} l
                    on l.loan_key = @loan_key
                   and {SubjectiveInputSql.EqualsVarchar("l", "loan_code", "r", "loan_code")}
                   and {SubjectiveInputSql.DimLoanIsCurrent("l")}
                """;
        }

        public async Task<IReadOnlyList<DefaultDateCaptureRowDto>> GetAsync(
            IReadOnlyList<int> loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default)
        {
            var statusFilter = LoanStatusFilterParser.Parse(statuses);
            string? loanStatusKeyColumn = null;
            if (statusFilter.HasFilter)
            {
                loanStatusKeyColumn = await GetLoanStatusKeyColumnAsync(cancellationToken);
                if (string.IsNullOrEmpty(loanStatusKeyColumn))
                {
                    throw new InvalidOperationException("Status filter requires loan_status_key on shared.dim_loan.");
                }
            }

            var sql = BuildListSql(loanAliasIds, statusFilter, loanStatusKeyColumn);

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
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                await using var command = new SqlCommand(_updateSql, connection);
                command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
                command.Parameters.AddWithValue(
                    "@default_date",
                    loan.DefaultDate.HasValue ? loan.DefaultDate.Value.Date : DBNull.Value);

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
            string? loanStatusKeyColumn)
        {
            var sql = new StringBuilder();
            sql.AppendLine($"""
                select {SubjectiveInputSql.LoanKeySelect()},
                       r.loan_code,
                       r.loan_description,
                       r.loan_alias_name,
                       r.loan_term_default_date,
                       r.default_date
                """);
            sql.Append(_listSqlFrom);

            sql.Append(" where m.loan_alias_id in (");
            sql.Append(string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}")));
            sql.Append(')');

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendSqlCondition(sql, "l", loanStatusKeyColumn, statusFilter, _tblDimStatus);
            }

            sql.AppendLine();
            sql.Append(" order by r.loan_alias_name, r.loan_code");
            return sql.ToString();
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
                Description = GetString(reader, "loan_description"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                LoanTermDefaultDate = GetNullableDate(reader, "loan_term_default_date"),
                DefaultDate = GetNullableDate(reader, "default_date")
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
