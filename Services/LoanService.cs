using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface ILoanService
    {
        Task<IReadOnlyList<LoanDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(LoanUpdateBatchRequest request, string auditDisplayName, CancellationToken cancellationToken = default);
    }

    public sealed class LoanService : ILoanService
    {
        private readonly string _connectionString;
        private readonly SubjectiveInputSql _sql;
        private readonly ILogger<LoanService> _logger;

        private bool _schemaProbed;
        private SubjectiveInputRelationshipAuditColumns _auditColumns = new();
        private string? _rankingColumn;
        private string? _dummyLoanLinkColumn;
        private string? _lateInterestApplicableColumn;
        private string? _lateInterestOffNoteColumn;
        private bool _lateInterestApplicableOnDimLoan;
        private bool _lateInterestOffNoteOnDimLoan;

        public LoanService(IConfiguration configuration, FabricWarehouseTables tables, ILogger<LoanService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _sql = new SubjectiveInputSql(tables);
        }

        public async Task<IReadOnlyList<LoanDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            var rows = new List<LoanDto>();

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

            _logger.LogInformation("Retrieved {Count} loan alias relationship rows.", rows.Count);
            return rows;
        }

        public async Task<bool> UpdateAsync(
            LoanUpdateBatchRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                if (!loan.LoanAliasKey.HasValue)
                {
                    continue;
                }

                var rowsChanged = loan.LoanKey > 0
                    ? await ExecuteUpdateAsync(BuildUpdateByLoanKeySql(), loan, auditDisplayName, connection, cancellationToken)
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

                if (_dummyLoanLinkColumn is not null && !string.IsNullOrWhiteSpace(loan.LoanCode))
                {
                    affectedRows += await ExecuteDummyLoanLinkUpdateAsync(
                        loan,
                        connection,
                        cancellationToken);
                }
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Loan alias relationship rows affected: {AffectedRows} by {AuditUser}",
                    affectedRows,
                    auditDisplayName);
                return true;
            }

            _logger.LogWarning("No loan rows updated.");
            return false;
        }

        private async Task<int> ExecuteUpdateAsync(
            string sql,
            LoanUpdateRequestDto loan,
            string auditDisplayName,
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(sql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
            command.Parameters.AddWithValue("@loan_code", loan.LoanCode?.Trim() ?? string.Empty);
            command.Parameters.AddWithValue("@loan_alias_key", loan.LoanAliasKey!.Value);
            command.Parameters.AddWithValue(
                "@loan_ranking",
                loan.LoanRanking.HasValue ? loan.LoanRanking.Value : DBNull.Value);

            if (_lateInterestApplicableColumn is not null && !_lateInterestApplicableOnDimLoan)
            {
                command.Parameters.AddWithValue(
                    "@is_loan_interest_applicable",
                    loan.IsLoanInterestApplicable.HasValue
                        ? loan.IsLoanInterestApplicable.Value
                        : DBNull.Value);
            }

            if (_lateInterestOffNoteColumn is not null && !_lateInterestOffNoteOnDimLoan)
            {
                command.Parameters.AddWithValue(
                    "@late_interest_off_note",
                    string.IsNullOrWhiteSpace(loan.LateInterestOffNote)
                        ? DBNull.Value
                        : loan.LateInterestOffNote.Trim());
            }

            _auditColumns.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<int> ExecuteDummyLoanLinkUpdateAsync(
            LoanUpdateRequestDto loan,
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            if (_dummyLoanLinkColumn is null)
            {
                return 0;
            }

            var sql = $"""
                update l
                set l.[{_dummyLoanLinkColumn}] = @dummy_loan_link
                from {_sql.SharedDimLoan} l
                where {SubjectiveInputSql.DimLoanIsCurrent("l")}
                  and (
                        (@loan_key > 0 and l.loan_key = @loan_key)
                        or (@loan_key <= 0 and cast(l.loan_code as varchar(100)) collate database_default = cast(@loan_code as varchar(100)) collate database_default)
                      )
                """;

            await using var command = new SqlCommand(sql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
            command.Parameters.AddWithValue("@loan_code", loan.LoanCode?.Trim() ?? string.Empty);
            command.Parameters.AddWithValue("@dummy_loan_link", loan.DummyLoanLink?.Trim() ?? string.Empty);

            return await command.ExecuteNonQueryAsync(cancellationToken);
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
            _rankingColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.LoanAliasRelationship,
                ["ranking", "loan_ranking"],
                cancellationToken);
            _dummyLoanLinkColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.SharedDimLoan,
                ["dummy_loan_link"],
                cancellationToken);

            _lateInterestApplicableColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.LoanAliasRelationship,
                ["is_loan_interest_applicable", "late_interest_applicable"],
                cancellationToken);
            _lateInterestApplicableOnDimLoan = false;
            if (_lateInterestApplicableColumn is null)
            {
                _lateInterestApplicableColumn = await DimLoanColumnProbe.FindFirstAsync(
                    _connectionString,
                    _sql.SharedDimLoan,
                    ["is_loan_interest_applicable", "late_interest_applicable"],
                    cancellationToken);
                _lateInterestApplicableOnDimLoan = _lateInterestApplicableColumn is not null;
            }

            _lateInterestOffNoteColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.LoanAliasRelationship,
                ["late_interest_off_note"],
                cancellationToken);
            _lateInterestOffNoteOnDimLoan = false;
            if (_lateInterestOffNoteColumn is null)
            {
                _lateInterestOffNoteColumn = await DimLoanColumnProbe.FindFirstAsync(
                    _connectionString,
                    _sql.SharedDimLoan,
                    ["late_interest_off_note"],
                    cancellationToken);
                _lateInterestOffNoteOnDimLoan = _lateInterestOffNoteColumn is not null;
            }

            _schemaProbed = true;
        }

        private string BuildRankingSelectExpression() =>
            _rankingColumn is not null
                ? $"r.[{_rankingColumn}]"
                : "cast(null as smallint)";

        private string BuildRankingUpdateSetClause() =>
            _rankingColumn is not null
                ? $", r.[{_rankingColumn}] = @loan_ranking"
                : string.Empty;

        private string BuildRelationshipAttributeUpdateSetClause()
        {
            var clauses = new List<string>();
            if (_lateInterestApplicableColumn is not null && !_lateInterestApplicableOnDimLoan)
            {
                clauses.Add($"r.[{_lateInterestApplicableColumn}] = @is_loan_interest_applicable");
            }

            if (_lateInterestOffNoteColumn is not null && !_lateInterestOffNoteOnDimLoan)
            {
                clauses.Add($"r.[{_lateInterestOffNoteColumn}] = @late_interest_off_note");
            }

            return clauses.Count == 0 ? string.Empty : ", " + string.Join(", ", clauses);
        }

        private string BuildDummyLoanLinkSelectExpression() =>
            _dummyLoanLinkColumn is not null
                ? $"isnull(l.[{_dummyLoanLinkColumn}], '')"
                : "''";

        private string BuildLateInterestApplicableSelectExpression()
        {
            if (_lateInterestApplicableColumn is null)
            {
                return "cast(null as bit)";
            }

            var tableAlias = _lateInterestApplicableOnDimLoan ? "l" : "r";
            return $"{tableAlias}.[{_lateInterestApplicableColumn}]";
        }

        private string BuildLateInterestOffNoteSelectExpression()
        {
            if (_lateInterestOffNoteColumn is null)
            {
                return "''";
            }

            var tableAlias = _lateInterestOffNoteOnDimLoan ? "l" : "r";
            return $"isnull({tableAlias}.[{_lateInterestOffNoteColumn}], '')";
        }

        private string BuildListSql() =>
            $"""
                select {SubjectiveInputSql.LoanKeySelect()},
                       r.loan_code,
                       loan_desc = isnull(r.loan_description, ''),
                       loan_alias_key = m.loan_alias_id,
                       loan_alias_name = isnull(r.loan_alias_name, ''),
                       investor_name = '',
                       loan_ranking = {BuildRankingSelectExpression()},
                       dummy_loan_link = {BuildDummyLoanLinkSelectExpression()},
                       is_loan_interest_applicable = {BuildLateInterestApplicableSelectExpression()},
                       late_interest_off_note = {BuildLateInterestOffNoteSelectExpression()},
                       user_updated_by = {_auditColumns.BuildSelectUpdatedByExpression()},
                       user_updated_date = {_auditColumns.BuildSelectUpdatedDtmExpression()}
                from {_sql.LoanAliasRelationship} r
                {_sql.LoanAliasMasterJoinOnName()}
                {_sql.SharedDimLoanJoinOnLoanCode()}
                order by r.loan_code
                """;

        private string BuildUpdateByLoanKeySql() =>
            $"""
                update r
                set loan_alias_name = m.loan_alias_name{BuildRankingUpdateSetClause()}{BuildRelationshipAttributeUpdateSetClause()}{_auditColumns.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
                inner join {_sql.LoanAliasMaster} m
                    on m.loan_alias_id = @loan_alias_key
                inner join {_sql.SharedDimLoan} l
                    on l.loan_key = @loan_key
                   and {SubjectiveInputSql.EqualsVarchar("l", "loan_code", "r", "loan_code")}
                   and {SubjectiveInputSql.DimLoanIsCurrent("l")}
                """;

        private string BuildUpdateByLoanCodeSql() =>
            $"""
                update r
                set loan_alias_name = m.loan_alias_name{BuildRankingUpdateSetClause()}{BuildRelationshipAttributeUpdateSetClause()}{_auditColumns.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
                inner join {_sql.LoanAliasMaster} m
                    on m.loan_alias_id = @loan_alias_key
                where cast(r.loan_code as varchar(100)) collate database_default = cast(@loan_code as varchar(100)) collate database_default
                """;

        private static LoanDto MapRow(SqlDataReader reader)
        {
            short? ranking = null;
            if (reader.TryGetOrdinal("loan_ranking", out var rankOrd) && !reader.IsDBNull(rankOrd))
            {
                ranking = Convert.ToInt16(reader.GetValue(rankOrd));
            }

            bool? interestApplicable = null;
            if (reader.TryGetOrdinal("is_loan_interest_applicable", out var intOrd) && !reader.IsDBNull(intOrd))
            {
                interestApplicable = reader.GetBoolean(intOrd);
            }

            DateTime? updatedDate = null;
            if (reader.TryGetOrdinal("user_updated_date", out var dateOrd) && !reader.IsDBNull(dateOrd))
            {
                updatedDate = DateTime.SpecifyKind(reader.GetDateTime(dateOrd), DateTimeKind.Utc);
            }

            return new LoanDto
            {
                LoanKey = reader.GetInt64OrDefault("loan_key"),
                LoanCode = reader.GetStringOrEmpty("loan_code"),
                LoanDesc = reader.GetStringOrEmpty("loan_desc"),
                LoanAliasKey = reader.GetNullableInt32("loan_alias_key"),
                LoanAliasName = reader.GetStringOrEmpty("loan_alias_name"),
                InvestorName = reader.GetStringOrEmpty("investor_name"),
                LoanRanking = ranking,
                DummyLoanLink = reader.GetStringOrEmpty("dummy_loan_link"),
                IsLoanInterestApplicable = interestApplicable,
                LateInterestOffNote = reader.GetStringOrEmpty("late_interest_off_note"),
                UserUpdatedBy = reader.GetStringOrEmpty("user_updated_by"),
                UserUpdatedDate = updatedDate
            };
        }
    }
}
