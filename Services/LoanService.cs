using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface ILoanService
    {
        Task<IReadOnlyList<LoanDto>> GetAllAsync(
            string? auditProfile = null,
            IReadOnlyList<string>? statuses = null,
            CancellationToken cancellationToken = default);
        Task<LoanLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(LoanUpdateBatchRequest request, string auditDisplayName, CancellationToken cancellationToken = default);
    }

    public sealed class LoanService : ILoanService
    {
        private readonly string _connectionString;
        private readonly SubjectiveInputSql _sql;
        private readonly INotificationService _notificationService;
        private readonly INonKsLoanAliasBridge _nonKsLoanAliasBridge;
        private readonly ILogger<LoanService> _logger;

        private bool _schemaProbed;
        private SubjectiveInputRelationshipAuditColumns _aliasAuditColumns = new();
        private SubjectiveInputRelationshipAuditColumns _attributeAuditColumns = new();
        private string? _rankingColumn;
        private string? _dummyLoanLinkColumn;
        private string? _lateInterestApplicableColumn;
        private string? _lateInterestOffNoteColumn;
        private string? _loanStatusKeyColumn;
        private string? _loanStatusDescriptionColumn;
        private string? _eslExtLoanCodeColumn;
        private string? _eslAliasColumn;
        private string? _eslDescriptionColumn;

        public LoanService(
            IConfiguration configuration,
            FabricWarehouseTables tables,
            INotificationService notificationService,
            INonKsLoanAliasBridge nonKsLoanAliasBridge,
            ILogger<LoanService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _notificationService = notificationService;
            _nonKsLoanAliasBridge = nonKsLoanAliasBridge;
            _logger = logger;
            _sql = new SubjectiveInputSql(tables);
        }

        public async Task<IReadOnlyList<LoanDto>> GetAllAsync(
            string? auditProfile = null,
            IReadOnlyList<string>? statuses = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);
            var useAliasList = IsAliasAuditProfile(auditProfile);
            var audit = ResolveAuditColumns(auditProfile, isAttributeUpdate: !useAliasList);
            var statusFilter = LoanStatusFilterParser.Parse(statuses);

            if (statusFilter.HasFilter && string.IsNullOrWhiteSpace(_loanStatusKeyColumn))
            {
                throw new InvalidOperationException(
                    "Status filter cannot be applied: shared.dim_loan has no funding_status_code "
                    + "(or equivalent) column.");
            }

            var rows = new List<LoanDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Alias Assignment: UNION list. Attribute Assignment: INNER JOIN dim_loan + status filter.
            var listSql = useAliasList
                ? BuildAliasAssignmentListSql(audit, statusFilter)
                : BuildAttributeAssignmentListSql(audit, statusFilter);

            await using var command = new SqlCommand(listSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            if (statusFilter.HasFilter)
            {
                LoanStatusFilterParser.AddParameters(command, statusFilter);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation(
                "Retrieved {Count} loan rows (audit={AuditScreen}, aliasList={AliasList}, statusFilter={HasStatus}).",
                rows.Count,
                audit.Screen,
                useAliasList,
                statusFilter.HasFilter);

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
            var isAliasAssignmentSave = IsAliasAuditProfile(request.AuditProfile)
                || request.Loans.All(loan => !IsAttributeUpdate(request, loan));

            foreach (var loan in request.Loans)
            {
                // A missing or non-positive alias key means "remove the assigned alias".
                var isClearingAlias = !loan.LoanAliasKey.HasValue || loan.LoanAliasKey.Value <= 0;
                var isAttributeUpdate = IsAttributeUpdate(request, loan);
                var audit = ResolveAuditColumns(request.AuditProfile, isAttributeUpdate);

                // Loan Alias Assignment: Non-KS writes external_serviced_loan; Yardi writes relationship.
                if (isAliasAssignmentSave && !isAttributeUpdate)
                {
                    var isNonKs = await _nonKsLoanAliasBridge.ExistsInExternalServicedLoanAsync(
                        connection,
                        loan.LoanCode,
                        cancellationToken);

                    if (isNonKs)
                    {
                        string? aliasName = null;
                        if (!isClearingAlias)
                        {
                            aliasName = await TryGetAliasNameByKeyAsync(
                                connection,
                                loan.LoanAliasKey!.Value,
                                cancellationToken);
                        }

                        await _nonKsLoanAliasBridge.SyncAliasToExternalServicedLoanAsync(
                            connection,
                            loan.LoanCode,
                            aliasName,
                            cancellationToken);
                        affectedRows++;
                        continue;
                    }
                }

                if (isClearingAlias)
                {
                    // Clearing alias always stamps loan-alias audit columns.
                    audit = _aliasAuditColumns;
                    var clearedRows = loan.LoanKey > 0
                        ? await ExecuteClearAliasAsync(
                            BuildClearAliasByLoanKeySql(audit), loan, audit, auditDisplayName, connection, cancellationToken)
                        : 0;

                    if (clearedRows == 0 && !string.IsNullOrWhiteSpace(loan.LoanCode))
                    {
                        clearedRows = await ExecuteClearAliasAsync(
                            BuildClearAliasByLoanCodeSql(audit), loan, audit, auditDisplayName, connection, cancellationToken);
                    }

                    affectedRows += clearedRows;
                    continue;
                }

                short? priorRanking = null;
                if (isAttributeUpdate && loan.LoanRanking.HasValue)
                {
                    priorRanking = await TryGetPriorRankingAsync(connection, loan, cancellationToken);
                }

                var updateSql = loan.LoanKey > 0
                    ? BuildUpdateByLoanKeySql(audit, isAttributeUpdate)
                    : BuildUpdateByLoanCodeSql(audit, isAttributeUpdate);

                var rowsChanged = loan.LoanKey > 0
                    ? await ExecuteUpdateAsync(
                        updateSql, loan, audit, isAttributeUpdate, auditDisplayName, connection, cancellationToken)
                    : 0;

                if (rowsChanged == 0 && !string.IsNullOrWhiteSpace(loan.LoanCode))
                {
                    rowsChanged = await ExecuteUpdateAsync(
                        BuildUpdateByLoanCodeSql(audit, isAttributeUpdate),
                        loan,
                        audit,
                        isAttributeUpdate,
                        auditDisplayName,
                        connection,
                        cancellationToken);
                }

                if (rowsChanged > 0 && isAttributeUpdate && loan.LoanRanking.HasValue)
                {
                    await _notificationService.CreateRankingUpdateAsync(
                        loan.LoanCode,
                        priorRanking,
                        loan.LoanRanking,
                        auditDisplayName,
                        cancellationToken);
                }
                else if (rowsChanged > 0 && !isAttributeUpdate)
                {
                    _logger.LogDebug(
                        "Alias-only update for {LoanCode}; stamped {AuditBy}/{AuditDtm}.",
                        loan.LoanCode,
                        audit.UpdatedByColumn,
                        audit.UpdatedDtmColumn);
                }

                affectedRows += rowsChanged;
                // Yardi alias assignment writes relationship only.
                // Non-KS is handled above via external_serviced_loan.
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

        private async Task TrySyncExternalAliasAsync(
            SqlConnection connection,
            string loanCode,
            string? loanAliasName,
            CancellationToken cancellationToken)
        {
            try
            {
                await _nonKsLoanAliasBridge.SyncAliasToExternalServicedLoanAsync(
                    connection,
                    loanCode,
                    loanAliasName,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to sync loan alias to Non-KS external_serviced_loan for {LoanCode}.",
                    loanCode);
            }
        }

        private async Task<string?> TryGetAliasNameByKeyAsync(
            SqlConnection connection,
            long loanAliasKey,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(
                $"""
                    select top 1 loan_alias_name
                    from {_sql.LoanAliasMaster}
                    where loan_alias_id = @loan_alias_key
                    """,
                connection);
            command.Parameters.AddWithValue("@loan_alias_key", loanAliasKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? null : Convert.ToString(result);
        }

        private async Task<int> ExecuteUpdateAsync(
            string sql,
            LoanUpdateRequestDto loan,
            SubjectiveInputRelationshipAuditColumns audit,
            bool isAttributeUpdate,
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

            if (isAttributeUpdate)
            {
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
            }

            audit.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<int> ExecuteClearAliasAsync(
            string sql,
            LoanUpdateRequestDto loan,
            SubjectiveInputRelationshipAuditColumns audit,
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
            audit.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);

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

            _aliasAuditColumns = await SubjectiveInputRelationshipAuditColumns.ProbeForScreenAsync(
                _connectionString,
                _sql.LoanAliasRelationship,
                SubjectiveInputAuditScreen.LoanAlias,
                cancellationToken);
            _attributeAuditColumns = await SubjectiveInputRelationshipAuditColumns.ProbeForScreenAsync(
                _connectionString,
                _sql.LoanAliasRelationship,
                SubjectiveInputAuditScreen.LoanAttribute,
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

            try
            {
                _loanStatusKeyColumn = await LoanDimStatusColumnResolver.ResolveAsync(
                    _connectionString,
                    _sql.SharedDimLoan,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve dim_loan funding status column.");
                _loanStatusKeyColumn = null;
            }

            _loanStatusDescriptionColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.SharedDimLoan,
                ["funding_status_description", "funding_status_desc", "loan_status_description"],
                cancellationToken);

            // Dev dim_loan may have one row per loan (no scd_cur_ind / is_current).
            await _sql.EnsureDimLoanCurrentIndicatorAsync(_connectionString, cancellationToken);

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

            _logger.LogInformation(
                "Loan relationship audit columns: alias={AliasBy}/{AliasDtm}, attribute={AttrBy}/{AttrDtm}, statusCol={StatusCol}, statusDesc={StatusDesc}",
                _aliasAuditColumns.UpdatedByColumn,
                _aliasAuditColumns.UpdatedDtmColumn,
                _attributeAuditColumns.UpdatedByColumn,
                _attributeAuditColumns.UpdatedDtmColumn,
                _loanStatusKeyColumn,
                _loanStatusDescriptionColumn);

            _schemaProbed = true;
        }

        private static bool IsAliasAuditProfile(string? auditProfile)
        {
            var normalized = auditProfile?.Trim().ToLowerInvariant();
            return normalized is null or "" or "loan_alias" or "alias" or "loan-alias";
        }

        private SubjectiveInputRelationshipAuditColumns ResolveAuditColumns(
            string? auditProfile,
            bool isAttributeUpdate)
        {
            var normalized = auditProfile?.Trim().ToLowerInvariant();
            if (normalized is "loan_attribute" or "attribute" or "loan-attribute")
            {
                return _attributeAuditColumns;
            }

            if (normalized is "loan_alias" or "alias" or "loan-alias")
            {
                return _aliasAuditColumns;
            }

            return isAttributeUpdate ? _attributeAuditColumns : _aliasAuditColumns;
        }

        private static bool IsAttributeUpdate(LoanUpdateBatchRequest request, LoanUpdateRequestDto loan)
        {
            var normalized = request.AuditProfile?.Trim().ToLowerInvariant();
            if (normalized is "loan_attribute" or "attribute" or "loan-attribute")
            {
                return true;
            }

            if (normalized is "loan_alias" or "alias" or "loan-alias")
            {
                return false;
            }

            // Infer from payload: attribute screen always sends ranking and/or late-interest fields.
            return loan.LoanRanking.HasValue || loan.IsLoanInterestApplicable.HasValue;
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

        /// <summary>
        /// Loan Alias Assignment list:
        /// relationship (Yardi) UNION external_serviced_loan (Non-KS not already in relationship),
        /// filtered per loan by dim_loan.funding_status_code / funding_status_description.
        /// </summary>
        private string BuildAliasAssignmentListSql(
            SubjectiveInputRelationshipAuditColumns audit,
            LoanStatusFilter statusFilter)
        {
            var sql = new StringBuilder();
            sql.AppendLine(
                $"""
                select loan_key = isnull(l.loan_key, 0),
                       r.loan_code,
                       loan_desc = isnull(r.loan_description, ''),
                       loan_alias_key = m.loan_alias_id,
                       loan_alias_name = isnull(r.loan_alias_name, ''),
                       investor_name = isnull(i.investor_name, ''),
                       investor_alias_name = isnull(d.investor_alias_name, ''),
                       loan_ranking = {BuildRankingSelectExpression()},
                       dummy_loan_link = {BuildDummyLoanLinkSelectExpression()},
                       is_loan_interest_applicable = {BuildLateInterestApplicableSelectExpression()},
                       late_interest_off_note = {BuildLateInterestOffNoteSelectExpression()},
                       user_updated_by = {audit.BuildSelectUpdatedByExpression()},
                       user_updated_date = {audit.BuildSelectUpdatedDtmExpression()},
                       is_non_ks = cast(0 as bit)
                from {_sql.LoanAliasRelationship} r
                left join {_sql.LoanAliasMaster} m on r.loan_alias_name = m.loan_alias_name
                {_sql.SharedDimLoanOuterApplyOnLoanCode("r", "l")}
                left join {_sql.MortgageDimInvestor} i on l.investor_code = i.investor_code
                {_sql.InvestorAliasRelationshipJoinOnInvestorCode("l", "d")}
                where 1 = 1
                """);

            if (statusFilter.HasFilter && !string.IsNullOrWhiteSpace(_loanStatusKeyColumn))
            {
                // Filter on current dim_loan row for this loan_code (not "any loan under the alias").
                LoanStatusFilterParser.AppendExistsSqlCondition(
                    sql,
                    "r",
                    _sql.SharedDimLoan,
                    _loanStatusKeyColumn!,
                    statusFilter,
                    _sql.DimStatus,
                    _loanStatusDescriptionColumn,
                    _sql.DimLoanCurrentIndicatorColumn);
            }

            if (_eslExtLoanCodeColumn is not null)
            {
                var descExpr = _eslDescriptionColumn is null
                    ? "cast('' as varchar(500))"
                    : $"isnull(cast(e.[{_eslDescriptionColumn}] as varchar(500)), '')";
                var aliasExpr = _eslAliasColumn is null
                    ? "cast('' as varchar(200))"
                    : $"isnull(cast(e.[{_eslAliasColumn}] as varchar(200)), '')";

                sql.AppendLine(
                    $"""
                    union
                    select loan_key = cast(0 as bigint),
                           loan_code = cast(e.[{_eslExtLoanCodeColumn}] as varchar(100)),
                           loan_desc = {descExpr},
                           loan_alias_key = m.loan_alias_id,
                           loan_alias_name = {aliasExpr},
                           investor_name = '',
                           investor_alias_name = '',
                           loan_ranking = cast(null as smallint),
                           dummy_loan_link = '',
                           is_loan_interest_applicable = cast(null as bit),
                           late_interest_off_note = '',
                           user_updated_by = '',
                           user_updated_date = cast(null as datetime2),
                           is_non_ks = cast(1 as bit)
                    from {_sql.ExternalServicedLoan} e
                    left join {_sql.LoanAliasMaster} m
                        on {aliasExpr} = m.loan_alias_name
                    where e.[{_eslExtLoanCodeColumn}] is not null
                      and ltrim(rtrim(cast(e.[{_eslExtLoanCodeColumn}] as varchar(100)))) <> ''
                      and not exists (
                          select 1
                          from {_sql.LoanAliasRelationship} r
                          where cast(r.loan_code as varchar(100)) collate database_default
                              = cast(e.[{_eslExtLoanCodeColumn}] as varchar(100)) collate database_default
                      )
                    """);

                // Non-KS rows typically have no dim_loan funding status — exclude them when a status is selected.
                if (statusFilter.HasFilter)
                {
                    sql.AppendLine("  and 1 = 0");
                }
            }

            sql.AppendLine("order by loan_code");
            return sql.ToString();
        }

        /// <summary>
        /// Loan Attribute Assignment list — relationship INNER JOIN current dim_loan,
        /// filtered per loan by funding_status_code / funding_status_description.
        /// </summary>
        private string BuildAttributeAssignmentListSql(
            SubjectiveInputRelationshipAuditColumns audit,
            LoanStatusFilter statusFilter)
        {
            var sql = new StringBuilder(
                $"""
                select loan_key = isnull(l.loan_key, 0),
                       r.loan_code,
                       loan_desc = isnull(r.loan_description, ''),
                       loan_alias_key = m.loan_alias_id,
                       loan_alias_name = isnull(r.loan_alias_name, ''),
                       investor_name = isnull(i.investor_name, ''),
                       investor_alias_name = isnull(d.investor_alias_name, ''),
                       loan_ranking = {BuildRankingSelectExpression()},
                       dummy_loan_link = {BuildDummyLoanLinkSelectExpression()},
                       is_loan_interest_applicable = {BuildLateInterestApplicableSelectExpression()},
                       late_interest_off_note = {BuildLateInterestOffNoteSelectExpression()},
                       user_updated_by = {audit.BuildSelectUpdatedByExpression()},
                       user_updated_date = {audit.BuildSelectUpdatedDtmExpression()},
                       is_non_ks = cast(0 as bit)
                from {_sql.LoanAliasRelationship} r
                inner join {_sql.SharedDimLoan} l
                    on {SubjectiveInputSql.EqualsVarchar("r", "loan_code", "l", "loan_code")}
                   and {_sql.DimLoanIsCurrent("l")}
                left join {_sql.LoanAliasMaster} m on r.loan_alias_name = m.loan_alias_name
                left join {_sql.MortgageDimInvestor} i on l.investor_code = i.investor_code
                {_sql.InvestorAliasRelationshipJoinOnInvestorCode("l", "d")}
                where 1 = 1
                """);

            if (statusFilter.HasFilter && !string.IsNullOrWhiteSpace(_loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendJoinedDimLoanStatusCondition(
                    sql,
                    "l",
                    _loanStatusKeyColumn!,
                    statusFilter,
                    _sql.DimStatus,
                    _loanStatusDescriptionColumn);
            }

            sql.AppendLine();
            sql.Append(" order by r.loan_code");
            return sql.ToString();
        }

        private string BuildListSql(SubjectiveInputRelationshipAuditColumns audit) =>
            $"""
                select loan_key = isnull(l.loan_key, 0),
                       r.loan_code,
                       loan_desc = isnull(r.loan_description, ''),
                       loan_alias_key = m.loan_alias_id,
                       loan_alias_name = isnull(r.loan_alias_name, ''),
                       investor_name = isnull(i.investor_name, ''),
                       investor_alias_name = isnull(d.investor_alias_name, ''),
                       loan_ranking = {BuildRankingSelectExpression()},
                       dummy_loan_link = {BuildDummyLoanLinkSelectExpression()},
                       is_loan_interest_applicable = {BuildLateInterestApplicableSelectExpression()},
                       late_interest_off_note = {BuildLateInterestOffNoteSelectExpression()},
                       user_updated_by = {audit.BuildSelectUpdatedByExpression()},
                       user_updated_date = {audit.BuildSelectUpdatedDtmExpression()},
                       is_non_ks = cast(0 as bit)
                from {_sql.LoanAliasRelationship} r
                left join {_sql.LoanAliasMaster} m on r.loan_alias_name = m.loan_alias_name
                left join {_sql.SharedDimLoan} l on r.loan_code = l.loan_code
                left join {_sql.MortgageDimInvestor} i on l.investor_code = i.investor_code
                {_sql.InvestorAliasRelationshipJoinOnInvestorCode("l", "d")}
                order by r.loan_code
                """;

        private string BuildUpdateByLoanKeySql(
            SubjectiveInputRelationshipAuditColumns audit,
            bool isAttributeUpdate) =>
            $"""
                update r
                set loan_alias_name = m.loan_alias_name{(isAttributeUpdate ? BuildRankingUpdateSetClause() + BuildDummyLoanLinkUpdateSetClause() + BuildRelationshipAttributeUpdateSetClause() : string.Empty)}{audit.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
                inner join {_sql.LoanAliasMaster} m
                    on m.loan_alias_id = @loan_alias_key
                inner join {_sql.SharedDimLoan} l
                    on l.loan_key = @loan_key
                   and l.loan_code = r.loan_code
                """;

        private string BuildUpdateByLoanCodeSql(
            SubjectiveInputRelationshipAuditColumns audit,
            bool isAttributeUpdate) =>
            $"""
                update r
                set loan_alias_name = m.loan_alias_name{(isAttributeUpdate ? BuildRankingUpdateSetClause() + BuildDummyLoanLinkUpdateSetClause() + BuildRelationshipAttributeUpdateSetClause() : string.Empty)}{audit.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
                inner join {_sql.LoanAliasMaster} m
                    on m.loan_alias_id = @loan_alias_key
                where r.loan_code = @loan_code
                """;

        private string BuildClearAliasByLoanKeySql(SubjectiveInputRelationshipAuditColumns audit) =>
            $"""
                update r
                set r.loan_alias_name = null{audit.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
                inner join {_sql.SharedDimLoan} l
                    on l.loan_key = @loan_key
                   and l.loan_code = r.loan_code
                """;

        private string BuildClearAliasByLoanCodeSql(SubjectiveInputRelationshipAuditColumns audit) =>
            $"""
                update r
                set r.loan_alias_name = null{audit.BuildUpdateSetClause()}
                from {_sql.LoanAliasRelationship} r
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
                InvestorAliasName = reader.GetStringOrEmpty("investor_alias_name"),
                LoanRanking = ranking,
                DummyLoanLink = reader.GetStringOrEmpty("dummy_loan_link"),
                IsLoanInterestApplicable = interestApplicable,
                LateInterestOffNote = reader.GetStringOrEmpty("late_interest_off_note"),
                UserUpdatedBy = reader.GetStringOrEmpty("user_updated_by"),
                UserUpdatedDate = updatedDate,
                IsNonKs = reader.TryGetOrdinal("is_non_ks", out var nonKsOrd)
                    && !reader.IsDBNull(nonKsOrd)
                    && Convert.ToBoolean(reader.GetValue(nonKsOrd))
            };
        }
    }
}
