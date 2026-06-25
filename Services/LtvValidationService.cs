using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface ILtvValidationService
    {
        Task<IReadOnlyList<LtvValidationRowDto>> GetAsync(
            IReadOnlyList<int> loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            LtvValidationBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);

        Task<bool> ConfirmAsync(
            LtvValidationConfirmRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);
    }

    public sealed class LtvValidationService : ILtvValidationService
    {
        private readonly string _listSqlBase;
        private readonly string _updateLtvSql;
        private readonly string _confirmLtvSql;
        private readonly string _loanEligibleSql;

        private readonly string _connectionString;
        private readonly string _tblSharedDimLoan;
        private readonly string _tblDimStatus;
        private readonly ILogger<LtvValidationService> _logger;
        private string? _loanStatusKeyColumn;

        public LtvValidationService(
            IConfiguration configuration,
            ILogger<LtvValidationService> logger,
            FabricWarehouseTables tables)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;

            var subjective = new SubjectiveInputSql(tables);
            _tblSharedDimLoan = subjective.SharedDimLoan;
            _tblDimStatus = subjective.DimStatus;

            var loanAliasRelationship = subjective.LoanAliasRelationship;
            var loanAliasMaster = subjective.LoanAliasMaster;

            // wh_gold1.subjective_input.loan_alias_relationship + shared.dim_loan (LTV Validation grid)
            _listSqlBase = $"""
                select loan_key = isnull(c.loan_key, 0),
                       parent_loan_id = isnull(c.parent_loan_code, ''),
                       child_loan_id = a.loan_code,
                       loan_desc = isnull(a.loan_description, ''),
                       loan_alias_name = isnull(a.loan_alias_name, ''),
                       investor_alias_name = isnull(d.investor_alias_name, ''),
                       b.security_value,
                       a.exposure,
                       a.ranking,
                       ltv = a.loan_to_value,
                       ai_commentary = a.ai_comments,
                       qr_slide_link = a.qr_slide_link
                from {loanAliasRelationship} a
                left join {loanAliasMaster} b
                    on a.loan_alias_name = b.loan_alias_name
                left join {_tblSharedDimLoan} c
                    on {SubjectiveInputSql.EqualsVarchar("a", "loan_code", "c", "loan_code")}
                   and {SubjectiveInputSql.DimLoanIsCurrent("c")}
                {subjective.InvestorAliasRelationshipJoinOnInvestorCode("c", "d")}
                """;

            _updateLtvSql = $"""
                update a
                set loan_to_value = @ltv
                from {loanAliasRelationship} a
                inner join {_tblSharedDimLoan} c
                    on c.loan_key = @loan_key
                   and {SubjectiveInputSql.EqualsVarchar("a", "loan_code", "c", "loan_code")}
                   and {SubjectiveInputSql.DimLoanIsCurrent("c")}
                """;

            _confirmLtvSql = $"""
                select 1
                from {loanAliasRelationship} a
                inner join {_tblSharedDimLoan} c
                    on c.loan_key = @loan_key
                   and {SubjectiveInputSql.EqualsVarchar("a", "loan_code", "c", "loan_code")}
                   and {SubjectiveInputSql.DimLoanIsCurrent("c")}
                where a.loan_to_value is not null
                """;

            _loanEligibleSql = $"""
                select 1
                from {loanAliasRelationship} a
                inner join {_tblSharedDimLoan} c
                    on c.loan_key = @loan_key
                   and {SubjectiveInputSql.EqualsVarchar("a", "loan_code", "c", "loan_code")}
                   and {SubjectiveInputSql.DimLoanIsCurrent("c")}
                """;
        }

        public async Task<IReadOnlyList<LtvValidationRowDto>> GetAsync(
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

            var rows = new List<LtvValidationRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation(
                "Retrieved {Count} LTV validation rows for {AliasCount} loan alias filter(s).",
                rows.Count,
                loanAliasIds.Count);

            return rows;
        }

        public async Task<bool> UpdateAsync(
            LtvValidationBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                ValidateLtv(loan.Ltv);
                if (loan.LoanKey <= 0)
                {
                    throw new InvalidOperationException("Loan key is required.");
                }

                if (!await IsLoanEligibleAsync(connection, loan.LoanKey, cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"Loan {loan.LoanKey} is not eligible for LTV validation.");
                }

                await using var command = new SqlCommand(_updateLtvSql, connection);
                command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
                command.Parameters.AddWithValue(
                    "@ltv",
                    loan.Ltv.HasValue ? loan.Ltv.Value : DBNull.Value);

                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Updated {AffectedRows} LTV validation loan rows.", affectedRows);
                return true;
            }

            _logger.LogWarning("No LTV validation loan rows updated.");
            return false;
        }

        public async Task<bool> ConfirmAsync(
            LtvValidationConfirmRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            if (request.LoanKeys.Count == 0)
            {
                throw new InvalidOperationException("At least one loan key is required.");
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var loanKey in request.LoanKeys)
            {
                if (loanKey <= 0)
                {
                    throw new InvalidOperationException("Loan key is required.");
                }

                if (!await IsLoanEligibleAsync(connection, loanKey, cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"Loan {loanKey} is not eligible for LTV validation.");
                }

                await using var command = new SqlCommand(_confirmLtvSql, connection);
                command.Parameters.AddWithValue("@loan_key", loanKey);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                if (result is null)
                {
                    throw new InvalidOperationException(
                        $"Loan {loanKey} has no AI-extracted LTV (loan_to_value) to confirm.");
                }

                affectedRows++;
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Confirmed AI LTV for {AffectedRows} loan row(s).", affectedRows);
                return true;
            }

            return false;
        }

        private string BuildListSql(
            IReadOnlyList<int> loanAliasIds,
            LoanStatusFilter statusFilter,
            string? loanStatusKeyColumn)
        {
            var sql = new StringBuilder(_listSqlBase);

            sql.Append(" where b.loan_alias_id in (");
            sql.Append(string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}")));
            sql.Append(')');

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendSqlCondition(sql, "c", loanStatusKeyColumn, statusFilter, _tblDimStatus);
            }

            sql.AppendLine();
            sql.Append(" order by b.loan_alias_name, a.loan_code");
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
                _tblSharedDimLoan,
                cancellationToken);

            return _loanStatusKeyColumn;
        }

        private async Task<bool> IsLoanEligibleAsync(
            SqlConnection connection,
            long loanKey,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(_loanEligibleSql, connection);
            command.Parameters.AddWithValue("@loan_key", loanKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        private static void AddLoanAliasParameters(SqlCommand command, IReadOnlyList<int> loanAliasIds)
        {
            for (var i = 0; i < loanAliasIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@loan_alias_id_{i}", loanAliasIds[i]);
            }
        }

        private static void ValidateLtv(decimal? ltv)
        {
            if (ltv is < 0 or > 100)
            {
                throw new InvalidOperationException("LTV must be between 0 and 100.");
            }
        }

        private static LtvValidationRowDto MapRow(SqlDataReader reader) =>
            new()
            {
                LoanKey = GetInt64(reader, "loan_key"),
                ParentLoanId = GetString(reader, "parent_loan_id"),
                ChildLoanId = GetString(reader, "child_loan_id"),
                Description = GetString(reader, "loan_desc"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                InvestorAliasName = GetString(reader, "investor_alias_name"),
                SecurityValue = GetNullableDecimal(reader, "security_value"),
                Exposure = GetNullableDecimal(reader, "exposure"),
                Ranking = GetNullableInt32(reader, "ranking"),
                Ltv = GetNullableDecimal(reader, "ltv"),
                AiCommentary = GetNullableString(reader, "ai_commentary")
            };

        private static long GetInt64(SqlDataReader reader, string name) =>
            reader.IsDBNull(reader.GetOrdinal(name))
                ? 0L
                : Convert.ToInt64(reader.GetValue(reader.GetOrdinal(name)));

        private static string GetString(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal)
                ? string.Empty
                : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        }

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

        private static decimal? GetNullableDecimal(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
        }

        private static int? GetNullableInt32(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static DateTime? GetNullableDateTime(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
    }
}
