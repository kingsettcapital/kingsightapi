using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface ILoanService
    {
        Task<IReadOnlyList<LoanDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<LoanLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(LoanUpdateBatchRequest request, string auditDisplayName, CancellationToken cancellationToken = default);
    }

    public sealed class LoanService : ILoanService
    {
        private readonly string _connectionString;
        private readonly SubjectiveInputSql _sql;
        private readonly INotificationService _notificationService;
        private readonly ILogger<LoanService> _logger;

        private bool _schemaProbed;
        private SubjectiveInputRelationshipAuditColumns _auditColumns = new();
        private string? _rankingColumn;
        private string? _dummyLoanLinkColumn;
        private string? _lateInterestApplicableColumn;
        private string? _lateInterestOffNoteColumn;

        public LoanService(
            IConfiguration configuration,
            FabricWarehouseTables tables,
            INotificationService notificationService,
            ILogger<LoanService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _notificationService = notificationService;
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
            var unresolvedLoanKeys = rows.Count(row => row.LoanKey == 0);
            if (unresolvedLoanKeys > 0)
            {
                _logger.LogWarning(
                    "{UnresolvedCount} of {TotalCount} loan rows did not resolve loan_key from {DimLoanTable}.",
                    unresolvedLoanKeys,
                    rows.Count,
                    _sql.SharedDimLoan);
            }

            return rows;
        }

        public async Task<LoanLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default)
        {
            var options = new List<LoanAliasOptionDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(
                $"""
                    select loan_alias_id, loan_alias_name
                    from {_sql.LoanAliasMaster}
                    order by loan_alias_name
                    """,
                connection);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                options.Add(new LoanAliasOptionDto
                {
                    LoanAliasId = reader.GetInt64OrDefault("loan_alias_id"),
                    LoanAliasName = reader.GetStringOrEmpty("loan_alias_name")
                });
            }

            return new LoanLookupsDto { LoanAliases = options };
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

                short? priorRanking = null;
                if (loan.LoanRanking.HasValue)
                {
                    priorRanking = await TryGetPriorRankingAsync(connection, loan, cancellationToken);
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

                if (rowsChanged > 0 && loan.LoanRanking.HasValue)
                {
                    await _notificationService.CreateRankingUpdateAsync(
                        loan.LoanCode,
                        priorRanking,
                        loan.LoanRanking,
                        auditDisplayName,
                        cancellationToken);
                }
                else if (rowsChanged > 0)
                {
                    _logger.LogDebug(
                        "Skipped ranking notification for {LoanCode}; loanRanking was not sent in the request.",
                        loan.LoanCode);
                }

                affectedRows += rowsChanged;
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

            if (_dummyLoanLinkColumn is not null)
            {
                command.Parameters.AddWithValue("@dummy_loan_link", loan.DummyLoanLink?.Trim() ?? string.Empty);
            }

            if (_lateInterestApplicableColumn is not null)
            {
                command.Parameters.AddWithValue(
                    "@is_loan_interest_applicable",
                    loan.IsLoanInterestApplicable.HasValue
                        ? loan.IsLoanInterestApplicable.Value
                        : DBNull.Value);
            }

            if (_lateInterestOffNoteColumn is not null)
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

        private async Task<short?> TryGetPriorRankingAsync(
            SqlConnection connection,
            LoanUpdateRequestDto loan,
            CancellationToken cancellationToken)
        {
            if (_rankingColumn is null)
            {
                return null;
            }

            var sql = loan.LoanKey > 0
                ? $"""
                  select top 1 r.[{_rankingColumn}]
                  from {_sql.LoanAliasRelationship} r
                  inner join {_sql.SharedDimLoan} l on l.loan_key = @loan_key and l.loan_code = r.loan_code
                  """
                : $"""
                  select top 1 r.[{_rankingColumn}]
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

            return Convert.ToInt16(result);
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
                _sql.LoanAliasRelationship,
                ["dummy_loan_link"],
                cancellationToken);
            _lateInterestApplicableColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.LoanAliasRelationship,
                ["late_interest_flag", "is_loan_interest_applicable", "late_interest_applicable"],
                cancellationToken);
            _lateInterestOffNoteColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.LoanAliasRelationship,
                ["late_interest_note", "late_interest_off_note"],
                cancellationToken);

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
            if (_lateInterestApplicableColumn is not null)
            {
                clauses.Add($"r.[{_lateInterestApplicableColumn}] = @is_loan_interest_applicable");
            }

            if (_lateInterestOffNoteColumn is not null)
            {
                clauses.Add($"r.[{_lateInterestOffNoteColumn}] = @late_interest_off_note");
            }

            return clauses.Count == 0 ? string.Empty : ", " + string.Join(", ", clauses);
        }

        private string BuildDummyLoanLinkUpdateSetClause() =>
            _dummyLoanLinkColumn is null
                ? string.Empty
                : $", r.[{_dummyLoanLinkColumn}] = @dummy_loan_link";

        private string BuildDummyLoanLinkSelectExpression() =>
            _dummyLoanLinkColumn is null
                ? "''"
                : $"isnull(r.[{_dummyLoanLinkColumn}], '')";

        private string BuildLateInterestApplicableSelectExpression() =>
            _lateInterestApplicableColumn is null
                ? "cast(null as bit)"
                : $"r.[{_lateInterestApplicableColumn}]";

        private string BuildLateInterestOffNoteSelectExpression() =>
            _lateInterestOffNoteColumn is null
                ? "''"
                : $"isnull(r.[{_lateInterestOffNoteColumn}], '')";

        private string BuildListSql() =>
            $"""
                select loan_key = isnull(l.loan_key, 0),
                       r.loan_code,
                       loan_desc = isnull(r.loan_description, ''),
                       loan_alias_key = m.loan_alias_id,
                       loan_alias_name = isnull(r.loan_alias_name, ''),
                       investor_name = isnull(i.investor_name, ''),
                       loan_ranking = {BuildRankingSelectExpression()},
                       dummy_loan_link = {BuildDummyLoanLinkSelectExpression()},
                       is_loan_interest_applicable = {BuildLateInterestApplicableSelectExpression()},
                       late_interest_off_note = {BuildLateInterestOffNoteSelectExpression()},
                       user_updated_by = {_auditColumns.BuildSelectUpdatedByExpression()},
                       user_updated_date = {_auditColumns.BuildSelectUpdatedDtmExpression()}
                from {_sql.LoanAliasRelationship} r
                left join {_sql.LoanAliasMaster} m on r.loan_alias_name = m.loan_alias_name
                left join {_sql.SharedDimLoan} l on r.loan_code = l.loan_code
                left join {_sql.MortgageDimInvestor} i on l.investor_code = i.investor_code
                order by r.loan_code
                """;

        private string BuildUpdateByLoanKeySql() =>
            $"""
                update r
                set loan_alias_name = m.loan_alias_name{BuildRankingUpdateSetClause()}{BuildDummyLoanLinkUpdateSetClause()}{BuildRelationshipAttributeUpdateSetClause()}{_auditColumns.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
                inner join {_sql.LoanAliasMaster} m
                    on m.loan_alias_id = @loan_alias_key
                inner join {_sql.SharedDimLoan} l
                    on l.loan_key = @loan_key
                   and l.loan_code = r.loan_code
                """;

        private string BuildUpdateByLoanCodeSql() =>
            $"""
                update r
                set loan_alias_name = m.loan_alias_name{BuildRankingUpdateSetClause()}{BuildDummyLoanLinkUpdateSetClause()}{BuildRelationshipAttributeUpdateSetClause()}{_auditColumns.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
                inner join {_sql.LoanAliasMaster} m
                    on m.loan_alias_id = @loan_alias_key
                where r.loan_code = @loan_code
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
                interestApplicable = reader.GetBooleanFromColumns("is_loan_interest_applicable");
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
                LoanAliasKey = reader.GetNullableInt64("loan_alias_key"),
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
