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
        /// <summary>
        /// Fabric round-trips are expensive; confirm many loan_codes per statement.
        /// </summary>
        private const int ConfirmLoanCodeBatchSize = 200;

        private readonly string _loanAliasRelationship;
        private readonly string _loanAliasMaster;
        private readonly string _tblSharedDimLoan;
        private readonly string _tblDimStatus;
        private readonly string _investorJoinSql;
        private readonly SubjectiveInputSql _subjectiveInputSql;
        private readonly string _loanEligibleSql;
        private readonly string _loanEligibleByKeySql;
        private readonly string _resolveLoanKeyByCodeSql;

        private readonly string _connectionString;
        private readonly INotificationService _notificationService;
        private readonly ILogger<LtvValidationService> _logger;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);

        private string? _loanStatusKeyColumn;
        private string? _loanStatusDescriptionColumn;
        private bool _loanStatusColumnsResolved;
        private LtvValidationSchema? _schema;

        public LtvValidationService(
            IConfiguration configuration,
            ILogger<LtvValidationService> logger,
            FabricWarehouseTables tables,
            INotificationService notificationService)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _notificationService = notificationService;
            _logger = logger;

            var subjective = new SubjectiveInputSql(tables);
            _subjectiveInputSql = subjective;
            _tblSharedDimLoan = subjective.SharedDimLoan;
            _tblDimStatus = subjective.DimStatus;
            _loanAliasRelationship = subjective.LoanAliasRelationship;
            _loanAliasMaster = subjective.LoanAliasMaster;
            _investorJoinSql = subjective.InvestorAliasRelationshipJoinOnInvestorCode("l", "d");

            _loanEligibleSql = $"""
                select 1
                from {_loanAliasRelationship} a
                where {SubjectiveInputSql.EqualsLoanCodeParam("a", "loan_code", "@loan_code")}
                """;

            _loanEligibleByKeySql = $"""
                select 1
                from {_loanAliasRelationship} a
                inner join {_tblSharedDimLoan} c
                    on c.loan_key = @loan_key
                   and {SubjectiveInputSql.EqualsLoanCode("a", "loan_code", "c", "loan_code")}
                """;

            _resolveLoanKeyByCodeSql = $"""
                select top (1) ck.loan_key
                from {_tblSharedDimLoan} ck
                where {SubjectiveInputSql.EqualsLoanCodeParam("ck", "loan_code", "@loan_code")}
                order by ck.loan_key desc
                """;
        }

        public async Task<IReadOnlyList<LtvValidationRowDto>> GetAsync(
            IReadOnlyList<int> loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default)
        {
            var schema = await GetSchemaAsync(cancellationToken);
            var statusFilter = LoanStatusFilterParser.Parse(statuses);
            string? loanStatusKeyColumn = null;
            string? loanStatusDescriptionColumn = null;
            if (statusFilter.HasFilter)
            {
                (loanStatusKeyColumn, loanStatusDescriptionColumn) =
                    await TryResolveLoanStatusColumnsAsync(cancellationToken);

                if (string.IsNullOrEmpty(loanStatusKeyColumn))
                {
                    throw new InvalidOperationException(
                        "Status filter cannot be applied: shared.dim_loan has no funding_status_code "
                        + "(or equivalent) column.");
                }
            }

            return await ExecuteListQueryAsync(
                schema,
                loanAliasIds,
                statusFilter,
                loanStatusKeyColumn,
                loanStatusDescriptionColumn,
                cancellationToken);
        }

        private async Task<IReadOnlyList<LtvValidationRowDto>> ExecuteListQueryAsync(
            LtvValidationSchema schema,
            IReadOnlyList<int> loanAliasIds,
            LoanStatusFilter statusFilter,
            string? loanStatusKeyColumn,
            string? loanStatusDescriptionColumn,
            CancellationToken cancellationToken)
        {
            var sql = BuildListSql(
                schema,
                loanAliasIds,
                statusFilter,
                loanStatusKeyColumn,
                loanStatusDescriptionColumn);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            AddLoanAliasParameters(command, loanAliasIds);
            LoanStatusFilterParser.AddParameters(command, statusFilter);

            try
            {
                return await ReadListRowsAsync(command, loanAliasIds, cancellationToken);
            }
            catch (SqlException ex) when (statusFilter.HasFilter)
            {
                _logger.LogError(
                    ex,
                    "LTV validation query failed with status filter (column={Column}).",
                    loanStatusKeyColumn);
                throw;
            }
        }

        private async Task<IReadOnlyList<LtvValidationRowDto>> ReadListRowsAsync(
            SqlCommand command,
            IReadOnlyList<int> loanAliasIds,
            CancellationToken cancellationToken)
        {
            var rows = new List<LtvValidationRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = MapRow(reader);
                if (row.LoanKey <= 0 && !string.IsNullOrWhiteSpace(row.LoanCode))
                {
                    _logger.LogWarning(
                        "LTV validation: no shared.dim_loan match for loan_code={LoanCode}, alias={LoanAliasName}",
                        row.LoanCode,
                        row.LoanAliasName);
                }

                rows.Add(row);
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
            var schema = await GetSchemaAsync(cancellationToken);
            if (schema.Optional.LtvColumn is null)
            {
                throw new InvalidOperationException(
                    "loan_alias_relationship has no LTV column (current_loan_to_value / loan_to_value / ltv). "
                    + "Run Scripts/Alter_loan_alias_relationship_ltv_validation.sql.");
            }

            var updateByLoanCodeSql = BuildUpdateByLoanCodeSql(schema);
            var auditUtc = DateTime.UtcNow;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                ValidateLtv(loan.Ltv);
                var loanCode = await ResolveLoanCodeAsync(connection, loan, cancellationToken);
                if (string.IsNullOrWhiteSpace(loanCode))
                {
                    throw new InvalidOperationException("Loan code is required.");
                }

                if (!await IsLoanEligibleByCodeAsync(connection, loanCode, cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"Loan {loanCode} is not eligible for LTV validation.");
                }

                await using var command = new SqlCommand(updateByLoanCodeSql, connection);
                command.Parameters.AddWithValue("@loan_code", loanCode);
                command.Parameters.AddWithValue(
                    "@ltv",
                    loan.Ltv.HasValue ? loan.Ltv.Value : DBNull.Value);
                command.Parameters.AddWithValue("@update_reason", ToDbString(loan.UpdateReason));
                command.Parameters.AddWithValue("@update_comment", ToDbString(loan.UpdateComment));
                schema.Audit.AddUpdateParameters(command, auditDisplayName, auditUtc);

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
            var schema = await GetSchemaAsync(cancellationToken);
            if (schema.Optional.LtvColumn is null)
            {
                throw new InvalidOperationException(
                    "loan_alias_relationship has no LTV column (current_loan_to_value / loan_to_value / ltv). "
                    + "Run Scripts/Alter_loan_alias_relationship_ltv_validation.sql.");
            }

            if (schema.Optional.IsConfirmedColumn is null)
            {
                throw new InvalidOperationException(
                    "loan_alias_relationship has no is_confirmed column. "
                    + "Confirm LTV must set is_confirmed = 'Y'. "
                    + "Run Scripts/Alter_loan_alias_relationship_ltv_validation.sql and restart the API.");
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var loanCodes = await ResolveConfirmLoanCodesAsync(connection, request, cancellationToken);
            if (loanCodes.Length == 0)
            {
                throw new InvalidOperationException("At least one loan code is required to confirm LTV.");
            }

            _logger.LogInformation(
                "Confirming LTV review for {LoanCodeCount} loan code(s) in batches of {BatchSize} (is_confirmed = 'Y').",
                loanCodes.Length,
                ConfirmLoanCodeBatchSize);

            var auditUtc = DateTime.UtcNow;
            var affectedRows = 0;
            var batchCount = 0;

            foreach (var batch in loanCodes.Chunk(ConfirmLoanCodeBatchSize))
            {
                batchCount++;
                var confirmSql = BuildConfirmByLoanCodesSql(schema, batch.Length);
                await using var command = new SqlCommand(confirmSql, connection);
                for (var i = 0; i < batch.Length; i++)
                {
                    command.Parameters.AddWithValue($"@loan_code_{i}", batch[i]);
                }

                schema.Audit.AddUpdateParameters(command, auditDisplayName, auditUtc);
                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (affectedRows == 0)
            {
                _logger.LogWarning("No loan_alias_relationship rows matched Confirm LTV (is_confirmed).");
                return false;
            }

            await _notificationService.CreateLtvReviewedAsync(auditDisplayName, cancellationToken);
            _logger.LogInformation(
                "Confirmed LTV review for {AffectedRows} loan row(s) via is_confirmed = 'Y' ({BatchCount} batch(es)).",
                affectedRows,
                batchCount);
            return true;
        }

        private async Task<string[]> ResolveConfirmLoanCodesAsync(
            SqlConnection connection,
            LtvValidationConfirmRequest request,
            CancellationToken cancellationToken)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var code in request.LoanCodes)
            {
                var trimmed = code?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    codes.Add(trimmed);
                }
            }

            var unresolvedKeys = request.LoanKeys.Where(key => key > 0).Distinct().ToArray();
            // SPA always sends loanCodes; skip key→code lookups when codes are already present.
            if (codes.Count > 0 || unresolvedKeys.Length == 0)
            {
                return codes.ToArray();
            }
            foreach (var keyBatch in unresolvedKeys.Chunk(ConfirmLoanCodeBatchSize))
            {
                var sql = new StringBuilder($"select loan_code from {_tblSharedDimLoan} where loan_key in (");
                sql.Append(string.Join(", ", keyBatch.Select((_, i) => $"@loan_key_{i}")));
                sql.Append(')');

                await using var command = new SqlCommand(sql.ToString(), connection);
                for (var i = 0; i < keyBatch.Length; i++)
                {
                    command.Parameters.AddWithValue($"@loan_key_{i}", keyBatch[i]);
                }

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var loanCode = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0))?.Trim();
                    if (!string.IsNullOrWhiteSpace(loanCode))
                    {
                        codes.Add(loanCode);
                    }
                }
            }

            return codes.ToArray();
        }

        private string BuildConfirmByLoanCodesSql(LtvValidationSchema schema, int loanCodeCount)
        {
            if (loanCodeCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(loanCodeCount));
            }

            var setClause = BuildConfirmSetClause(schema);
            if (string.IsNullOrWhiteSpace(setClause))
            {
                throw new InvalidOperationException(
                    "Confirm LTV has nothing to update (is_confirmed / audit columns missing).");
            }

            var inList = string.Join(", ", Enumerable.Range(0, loanCodeCount).Select(i => $"@loan_code_{i}"));

            // One statement per batch instead of one UPDATE per loan_code.
            return $"""
                update a
                set {setClause}
                from {_loanAliasRelationship} a
                where a.loan_code in ({inList})
                """;
        }

        private string BuildConfirmSetClause(LtvValidationSchema schema)
        {
            var confirmedSet = schema.Optional.BuildConfirmUpdateSetClause("a");
            var auditSet = schema.Audit.BuildUpdateSetClause(); // leading ", col = @param" when present

            if (string.IsNullOrWhiteSpace(confirmedSet) && string.IsNullOrWhiteSpace(auditSet))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(confirmedSet))
            {
                return auditSet.TrimStart(',', ' ');
            }

            return string.IsNullOrWhiteSpace(auditSet)
                ? confirmedSet
                : confirmedSet + auditSet;
        }

        private async Task<LtvValidationSchema> GetSchemaAsync(CancellationToken cancellationToken)
        {
            if (_schema is not null)
            {
                return _schema;
            }

            await _schemaLock.WaitAsync(cancellationToken);
            try
            {
                if (_schema is not null)
                {
                    return _schema;
                }

                var optional = await LtvValidationOptionalColumns.ProbeAsync(
                    _connectionString,
                    _loanAliasRelationship,
                    cancellationToken);
                var audit = await SubjectiveInputRelationshipAuditColumns.ProbeForScreenAsync(
                    _connectionString,
                    _loanAliasRelationship,
                    SubjectiveInputAuditScreen.Ltv,
                    cancellationToken);
                var qrSlideLink = await DimLoanColumnProbe.FindFirstAsync(
                    _connectionString,
                    _loanAliasRelationship,
                    ["qr_slide_link"],
                    cancellationToken);

                _schema = new LtvValidationSchema(optional, audit, qrSlideLink);
                await _subjectiveInputSql.EnsureDimLoanCurrentIndicatorAsync(
                    _connectionString,
                    cancellationToken);
                _logger.LogInformation(
                    "LTV validation schema: currentLtv={Ltv}, priorLtv={Prior}, updateReason={Reason}, aiComments={Ai}, qrSlide={Qr}, isConfirmed={Confirmed}, auditBy={AuditBy}.",
                    optional.LtvColumn ?? "(none)",
                    optional.PriorLtvColumn ?? "(none)",
                    optional.UpdateReason ?? "(none)",
                    optional.AiComments ?? "(none)",
                    qrSlideLink ?? "(none)",
                    optional.IsConfirmedColumn ?? "(none)",
                    audit.UpdatedByColumn ?? "(none)");
                if (optional.LtvColumn is null)
                {
                    _logger.LogWarning(
                        "loan_alias_relationship has no LTV column (current_loan_to_value / loan_to_value / ltv). "
                        + "LTV screen will load with null LTV values; run Scripts/Alter_loan_alias_relationship_ltv_validation.sql for saves and confirm.");
                }
                if (optional.IsConfirmedColumn is null)
                {
                    _logger.LogWarning(
                        "loan_alias_relationship has no is_confirmed column. "
                        + "Confirm LTV will not set the report confirmed flag until the column exists.");
                }

                return _schema;
            }
            finally
            {
                _schemaLock.Release();
            }
        }

        private string BuildListSql(
            LtvValidationSchema schema,
            IReadOnlyList<int> loanAliasIds,
            LoanStatusFilter statusFilter,
            string? loanStatusKeyColumn,
            string? loanStatusDescriptionColumn)
        {
            // Aligns with warehouse list shape: relationship + master + dim_loan + investor_alias.
            // Status filter uses current dim_loan.funding_status_code / funding_status_description.
            var sql = new StringBuilder($"""
                select {SubjectiveInputSql.LoanKeySelect("a", "l")},
                       parent_loan_code = isnull(l.parent_loan_code, ''),
                       loan_code = a.loan_code,
                       loan_name = isnull(a.loan_description, ''),
                       loan_alias_name = isnull(a.loan_alias_name, ''),
                       investor_alias_name = isnull(d.investor_alias_name, ''),
                       b.security_value,
                       a.exposure,
                       a.ranking,
                       {schema.Optional.BuildLtvSelectExpression("a")},
                       {schema.Optional.BuildPriorLtvSelectExpression("a")},
                       {schema.QrSlideLinkSelect},
                       user_updated_by = {schema.Audit.BuildSelectUpdatedByExpression("a")},
                       user_updated_date = {schema.Audit.BuildSelectUpdatedDtmExpression("a")}
                       {schema.Optional.BuildOptionalSelectFragment("a")}
                from {_loanAliasRelationship} a
                left join {_loanAliasMaster} b
                    on a.loan_alias_name = b.loan_alias_name
                {_subjectiveInputSql.SharedDimLoanOuterApplyOnLoanCode("a", "l")}
                {_investorJoinSql}
                """);

            sql.AppendLine();
            sql.Append(" where 1 = 1");
            AppendLoanAliasFilter(sql, loanAliasIds);

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendExistsSqlCondition(
                    sql,
                    "a",
                    _subjectiveInputSql.SharedDimLoan,
                    loanStatusKeyColumn,
                    statusFilter,
                    _tblDimStatus,
                    loanStatusDescriptionColumn,
                    _subjectiveInputSql.DimLoanCurrentIndicatorColumn);
            }

            sql.AppendLine();
            sql.Append(" order by isnull(a.loan_alias_name, ''), a.loan_code");
            return sql.ToString();
        }

        private static void AppendLoanAliasFilter(StringBuilder sql, IReadOnlyList<int> loanAliasIds)
        {
            if (loanAliasIds.Count == 0)
            {
                return;
            }

            if (loanAliasIds.Count == 1)
            {
                sql.Append(" and b.loan_alias_id = @loan_alias_id_0");
                return;
            }

            sql.Append(" and b.loan_alias_id in (");
            sql.Append(string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}")));
            sql.Append(')');
        }

        private string BuildUpdateByLoanCodeSql(LtvValidationSchema schema)
        {
            var ltvSet = schema.Optional.BuildLtvUpdateSetClause("a");
            if (string.IsNullOrWhiteSpace(ltvSet))
            {
                throw new InvalidOperationException(
                    "loan_alias_relationship has no LTV column (current_loan_to_value / loan_to_value / ltv). "
                    + "Run Scripts/Alter_loan_alias_relationship_ltv_validation.sql.");
            }

            var auditSet = schema.Audit.BuildUpdateSetClause();
            var optionalSet = schema.Optional.BuildOptionalUpdateSetClause("a");

            return $"""
                update a
                set {ltvSet}{optionalSet}{auditSet}
                from {_loanAliasRelationship} a
                where {SubjectiveInputSql.EqualsLoanCodeParam("a", "loan_code", "@loan_code")}
                """;
        }

        private string BuildUpdateSql(LtvValidationSchema schema)
        {
            var ltvSet = schema.Optional.BuildLtvUpdateSetClause("a");
            if (string.IsNullOrWhiteSpace(ltvSet))
            {
                throw new InvalidOperationException(
                    "loan_alias_relationship has no LTV column (current_loan_to_value / loan_to_value / ltv). "
                    + "Run Scripts/Alter_loan_alias_relationship_ltv_validation.sql.");
            }

            var auditSet = schema.Audit.BuildUpdateSetClause();
            var optionalSet = schema.Optional.BuildOptionalUpdateSetClause("a");

            return $"""
                update a
                set {ltvSet}{optionalSet}{auditSet}
                from {_loanAliasRelationship} a
                inner join {_tblSharedDimLoan} c
                    on c.loan_key = @loan_key
                   and {SubjectiveInputSql.EqualsLoanCode("a", "loan_code", "c", "loan_code")}
                """;
        }

        private async Task<(string? KeyColumn, string? DescriptionColumn)> TryResolveLoanStatusColumnsAsync(
            CancellationToken cancellationToken)
        {
            if (_loanStatusColumnsResolved)
            {
                return (_loanStatusKeyColumn, _loanStatusDescriptionColumn);
            }

            try
            {
                _loanStatusKeyColumn = await LoanDimStatusColumnResolver.ResolveAsync(
                    _connectionString,
                    _tblSharedDimLoan,
                    cancellationToken);

                _loanStatusDescriptionColumn = await DimLoanColumnProbe.FindFirstAsync(
                    _connectionString,
                    _tblSharedDimLoan,
                    ["funding_status_description", "funding_status_desc", "loan_status_description"],
                    cancellationToken);

                _logger.LogInformation(
                    "Using shared.dim_loan.{Column} (desc={Desc}) for LTV validation status filter.",
                    _loanStatusKeyColumn,
                    _loanStatusDescriptionColumn ?? "(none)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "LTV validation status filter skipped; shared.dim_loan has no status column. "
                    + "Rows are loaded from subjective_input without dim_loan status filtering.");
                _loanStatusKeyColumn = null;
                _loanStatusDescriptionColumn = null;
            }

            _loanStatusColumnsResolved = true;
            return (_loanStatusKeyColumn, _loanStatusDescriptionColumn);
        }

        private async Task<bool> IsLoanEligibleByCodeAsync(
            SqlConnection connection,
            string loanCode,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(_loanEligibleSql, connection);
            command.Parameters.AddWithValue("@loan_code", loanCode);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        private async Task<string?> ResolveLoanCodeAsync(
            SqlConnection connection,
            LtvValidationUpdateItem loan,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(loan.LoanCode))
            {
                return loan.LoanCode.Trim();
            }

            if (loan.LoanKey <= 0)
            {
                return null;
            }

            var sql = $"select loan_code from {_tblSharedDimLoan} where loan_key = @loan_key";
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? null : Convert.ToString(result)?.Trim();
        }

        private async Task<bool> IsLoanEligibleAsync(
            SqlConnection connection,
            long loanKey,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(_loanEligibleByKeySql, connection);
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
            // Current and Prior LTV are percent points and may exceed 100 (underwater / high-risk).
            if (ltv is < 0 or > 999)
            {
                throw new InvalidOperationException("LTV must be between 0 and 999.");
            }
        }

        private static object ToDbString(string? value) =>
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

        private static LtvValidationRowDto MapRow(SqlDataReader reader) =>
            new()
            {
                LoanKey = GetInt64(reader, "loan_key"),
                ParentLoanCode = GetNullableString(reader, "parent_loan_code"),
                LoanCode = GetString(reader, "loan_code"),
                LoanName = GetString(reader, "loan_name"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                InvestorAliasName = GetString(reader, "investor_alias_name"),
                SecurityValue = GetNullableDecimal(reader, "security_value"),
                Exposure = GetNullableDecimal(reader, "exposure"),
                Ranking = GetNullableInt32(reader, "ranking"),
                Ltv = GetNullableDecimal(reader, "ltv"),
                PriorLtv = GetNullableDecimal(reader, "prior_ltv"),
                UpdateReason = GetNullableString(reader, "update_reason"),
                UpdateComment = GetNullableString(reader, "update_comment"),
                AiComments = GetNullableString(reader, "ai_comments"),
                AiConfidenceScore = GetNullableDecimal(reader, "ai_confidence_score")
                    ?? ParseNullableDecimal(GetNullableString(reader, "ai_comments")),
                QrSlideLink = GetNullableString(reader, "qr_slide_link"),
                UserUpdatedBy = GetNullableString(reader, "user_updated_by"),
                UserUpdatedDate = GetNullableDateTime(reader, "user_updated_date")
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

        private static decimal? ParseNullableDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return decimal.TryParse(value, out var parsed) ? parsed : null;
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
            return reader.IsDBNull(ordinal)
                ? null
                : DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
        }

        private sealed class LtvValidationSchema
        {
            public LtvValidationSchema(
                LtvValidationOptionalColumns optional,
                SubjectiveInputRelationshipAuditColumns audit,
                string? qrSlideLinkColumn)
            {
                Optional = optional;
                Audit = audit;
                QrSlideLinkSelect = qrSlideLinkColumn is null
                    ? "cast(null as varchar(500)) as qr_slide_link"
                    : $"a.[{qrSlideLinkColumn}] as qr_slide_link";
            }

            public LtvValidationOptionalColumns Optional { get; }
            public SubjectiveInputRelationshipAuditColumns Audit { get; }
            public string QrSlideLinkSelect { get; }
        }
    }
}
