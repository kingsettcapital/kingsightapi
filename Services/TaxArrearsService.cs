using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface ITaxArrearsService
    {
        TaxArrearsLookupsDto GetLookups();

        Task<IReadOnlyList<TaxArrearsRowDto>> GetAsync(
            IReadOnlyList<int>? loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default);

        Task<TaxArrearsRowDto> CreateAsync(
            TaxArrearsCreateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            TaxArrearsBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);
    }

    public sealed class TaxArrearsService : ITaxArrearsService
    {
        private readonly string _nextTaxArrearKeySql;
        private readonly string _loanEligibleByCodeSql;

        private readonly string _connectionString;
        private readonly SubjectiveInputSql _sql;
        private readonly string _tblDimLoan;
        private readonly string _tblLoanAliasMaster;
        private readonly string _tblLoanAliasRelationship;
        private readonly string _tblDimStatus;
        private readonly string _tblTaxArrears;
        private readonly ILogger<TaxArrearsService> _logger;

        private string? _loanStatusKeyColumn;
        private bool? _tableAvailable;
        private bool _schemaProbed;
        private bool _hasTaxArrearKeyColumn;
        private SubjectiveInputRelationshipAuditColumns _auditColumns = new();

        public TaxArrearsService(
            IConfiguration configuration,
            ILogger<TaxArrearsService> logger,
            FabricWarehouseTables tables)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _sql = new SubjectiveInputSql(tables);
            _tblDimLoan = _sql.SharedDimLoan;
            _tblLoanAliasMaster = _sql.LoanAliasMaster;
            _tblLoanAliasRelationship = _sql.LoanAliasRelationship;
            _tblDimStatus = _sql.DimStatus;
            _tblTaxArrears = _sql.LoanTaxDetails;

            _loanEligibleByCodeSql = $"""
                select r.loan_code
                from {_tblLoanAliasRelationship} r
                where r.loan_code = @loan_code
                """;

            _nextTaxArrearKeySql = $"""
                select isnull(max(tax_arrear_key), 0) + 1
                from {_tblTaxArrears}
                """;
        }

        public TaxArrearsLookupsDto GetLookups()
        {
            var currentYear = DateTime.UtcNow.Year;
            var years = new List<string>();
            for (var year = currentYear + 1; year >= currentYear - 15; year--)
            {
                years.Add(year.ToString());
            }

            return new TaxArrearsLookupsDto { TaxYears = years };
        }

        public async Task<IReadOnlyList<TaxArrearsRowDto>> GetAsync(
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
                _logger.LogError(
                    ex,
                    "Tax arrears query failed with status filter (column={Column}).",
                    loanStatusKeyColumn);
                throw;
            }
        }

        public async Task<TaxArrearsRowDto> CreateAsync(
            TaxArrearsCreateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            ValidateCreateRequest(request);
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var loanCode = await ResolveLoanCodeAsync(connection, request, cancellationToken);
            if (string.IsNullOrWhiteSpace(loanCode))
            {
                throw new InvalidOperationException("Loan code could not be resolved for the new tax arrears record.");
            }

            if (!await IsLoanEligibleByCodeAsync(connection, loanCode, cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Loan {loanCode} is not assigned in loan_alias_relationship.");
            }

            if (await ExistsLoanMemoDateAndYearAsync(
                    connection,
                    loanCode,
                    request.TaxMemoDate,
                    request.TaxYear,
                    cancellationToken))
            {
                var dateLabel = request.TaxMemoDate!.Value.ToString("yyyy-MM-dd");
                var yearLabel = string.IsNullOrWhiteSpace(request.TaxYear) ? "(none)" : request.TaxYear.Trim();
                throw new InvalidOperationException(
                    $"Tax arrears for loan {loanCode}, tax memo date {dateLabel}, and tax year {yearLabel} already exists. " +
                    "Each loan can have only one row per tax memo date and tax year combination.");
            }

            long taxArrearKey = 0;
            if (_hasTaxArrearKeyColumn)
            {
                taxArrearKey = await GetNextTaxArrearKeyAsync(connection, cancellationToken);
            }

            await using (var insertCommand = new SqlCommand(BuildInsertSql(), connection))
            {
                if (_hasTaxArrearKeyColumn)
                {
                    insertCommand.Parameters.AddWithValue("@tax_arrear_key", taxArrearKey);
                }

                AddTaxArrearParameters(insertCommand, loanCode, request);
                _auditColumns.AddUpdateParameters(insertCommand, auditDisplayName, DateTime.UtcNow);
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            var row = _hasTaxArrearKeyColumn && taxArrearKey > 0
                ? await ReadByKeyAsync(connection, taxArrearKey, cancellationToken)
                : await ReadByLoanMemoDateAndYearAsync(
                    connection,
                    loanCode,
                    request.TaxMemoDate,
                    request.TaxYear,
                    cancellationToken);

            if (row is null)
            {
                throw new InvalidOperationException("Tax arrears record was created but could not be read back.");
            }

            _logger.LogInformation(
                "Created tax arrears record for loan {LoanCode} (key={TaxArrearKey}).",
                loanCode,
                taxArrearKey);
            return row;
        }

        public async Task<bool> UpdateAsync(
            TaxArrearsBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var item in request.TaxArrears)
            {
                ValidateUpdateItem(item);

                if (string.IsNullOrWhiteSpace(item.LoanCode))
                {
                    continue;
                }

                // Natural key is (loan_code, tax_memo_date, tax_year).
                var rowsChanged = await UpdateByNaturalKeyAsync(
                    item,
                    auditDisplayName,
                    connection,
                    cancellationToken);
                affectedRows += rowsChanged;
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Updated {AffectedRows} tax arrears rows.", affectedRows);
                return true;
            }

            _logger.LogWarning("No tax arrears rows updated.");
            return false;
        }

        /// <summary>
        /// Updates by natural key (loan_code + tax_memo_date + tax_year). If legacy duplicate rows
        /// share that key, they are collapsed to a single row before applying the change.
        /// </summary>
        private async Task<int> UpdateByNaturalKeyAsync(
            TaxArrearsUpdateItem item,
            string auditDisplayName,
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            var loanCode = item.LoanCode!.Trim();
            var originalMemoDate = item.OriginalTaxMemoDate?.Date ?? item.TaxMemoDate?.Date;
            var targetMemoDate = item.TaxMemoDate?.Date;
            var originalYear = NormalizeOptional(item.OriginalTaxYear ?? item.TaxYear);
            var targetYear = NormalizeOptional(item.TaxYear);
            var keyChanging =
                originalMemoDate != targetMemoDate
                || !string.Equals(originalYear, targetYear, StringComparison.OrdinalIgnoreCase);

            if (keyChanging
                && await ExistsLoanMemoDateAndYearAsync(
                    connection,
                    loanCode,
                    item.TaxMemoDate,
                    item.TaxYear,
                    cancellationToken))
            {
                var dateLabel = targetMemoDate?.ToString("yyyy-MM-dd") ?? "(none)";
                var yearLabel = targetYear ?? "(none)";
                throw new InvalidOperationException(
                    $"Tax arrears for loan {loanCode}, tax memo date {dateLabel}, and tax year {yearLabel} already exists. " +
                    "Each loan can have only one row per tax memo date and tax year combination.");
            }

            var matchCount = await CountByNaturalKeyAsync(
                connection,
                loanCode,
                originalMemoDate,
                originalYear,
                cancellationToken);

            if (matchCount == 0)
            {
                throw new InvalidOperationException(
                    $"No tax arrears row found for loan {loanCode}, tax memo date {originalMemoDate?.ToString("yyyy-MM-dd") ?? "(none)"}, and tax year {originalYear ?? "(none)"}.");
            }

            if (matchCount > 1)
            {
                _logger.LogWarning(
                    "Collapsing {Count} duplicate tax arrears rows for loan {LoanCode} memo date {TaxMemoDate} year {TaxYear} into one.",
                    matchCount,
                    loanCode,
                    originalMemoDate,
                    originalYear);
                await DeleteByNaturalKeyAsync(
                    connection,
                    loanCode,
                    originalMemoDate,
                    originalYear,
                    cancellationToken);

                await InsertUpdatedRowAsync(connection, loanCode, item, auditDisplayName, cancellationToken);
                return 1;
            }

            return await ExecuteUpdateAsync(
                BuildUpdateByLoanCodeSql(),
                item,
                auditDisplayName,
                connection,
                cancellationToken);
        }

        private async Task<int> ExecuteUpdateAsync(
            string sql,
            TaxArrearsUpdateItem item,
            string auditDisplayName,
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(sql, connection);
            // tax_arrear_key is optional legacy support only; natural key is loan_code + tax_memo_date + tax_year.
            if (item.TaxArrearKey > 0 && sql.Contains("@tax_arrear_key", StringComparison.Ordinal))
            {
                command.Parameters.AddWithValue("@tax_arrear_key", item.TaxArrearKey);
            }

            command.Parameters.AddWithValue("@loan_code", item.LoanCode?.Trim() ?? string.Empty);
            command.Parameters.AddWithValue(
                "@original_tax_memo_date",
                item.OriginalTaxMemoDate.HasValue
                    ? item.OriginalTaxMemoDate.Value.Date
                    : item.TaxMemoDate.HasValue
                        ? item.TaxMemoDate.Value.Date
                        : DBNull.Value);
            command.Parameters.AddWithValue(
                "@original_tax_year",
                ToDbValue(NormalizeOptional(item.OriginalTaxYear ?? item.TaxYear)));
            command.Parameters.AddWithValue(
                "@tax_memo_date",
                item.TaxMemoDate.HasValue ? item.TaxMemoDate.Value.Date : DBNull.Value);
            command.Parameters.AddWithValue(
                "@tax_arrears",
                item.TaxArrears.HasValue ? item.TaxArrears.Value : DBNull.Value);
            command.Parameters.AddWithValue("@tax_year", ToDbValue(NormalizeOptional(item.TaxYear)));
            command.Parameters.AddWithValue("@notes", ToDbValue(NormalizeOptional(item.Notes)));
            _auditColumns.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task InsertUpdatedRowAsync(
            SqlConnection connection,
            string loanCode,
            TaxArrearsUpdateItem item,
            string auditDisplayName,
            CancellationToken cancellationToken)
        {
            var createRequest = new TaxArrearsCreateRequest
            {
                LoanCode = loanCode,
                TaxMemoDate = item.TaxMemoDate,
                TaxArrears = item.TaxArrears,
                TaxYear = item.TaxYear,
                Notes = item.Notes,
                UserUpdatedBy = auditDisplayName,
            };

            long taxArrearKey = 0;
            if (_hasTaxArrearKeyColumn)
            {
                taxArrearKey = await GetNextTaxArrearKeyAsync(connection, cancellationToken);
            }

            await using var insertCommand = new SqlCommand(BuildInsertSql(), connection);
            if (_hasTaxArrearKeyColumn)
            {
                insertCommand.Parameters.AddWithValue("@tax_arrear_key", taxArrearKey);
            }

            AddTaxArrearParameters(insertCommand, loanCode, createRequest);
            _auditColumns.AddUpdateParameters(insertCommand, auditDisplayName, DateTime.UtcNow);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        private static string NaturalKeyWhereClause(string tableAlias = "")
        {
            var prefix = string.IsNullOrEmpty(tableAlias) ? string.Empty : $"{tableAlias}.";
            return $"""
                (
                      (@tax_memo_date is null and {prefix}tax_memo_date is null)
                   or cast({prefix}tax_memo_date as date) = cast(@tax_memo_date as date)
                )
                and isnull(cast({prefix}tax_year as varchar(20)), '') = isnull(cast(@tax_year as varchar(20)), '')
                """;
        }

        private async Task<int> CountByNaturalKeyAsync(
            SqlConnection connection,
            string loanCode,
            DateTime? taxMemoDate,
            string? taxYear,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                select count(1)
                from {_tblTaxArrears}
                where loan_code = @loan_code
                  and {NaturalKeyWhereClause()}
                """;
            await using var command = new SqlCommand(sql, connection);
            AddNaturalKeyParameters(command, loanCode, taxMemoDate, taxYear);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is int count ? count : Convert.ToInt32(result);
        }

        private async Task DeleteByNaturalKeyAsync(
            SqlConnection connection,
            string loanCode,
            DateTime? taxMemoDate,
            string? taxYear,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                delete from {_tblTaxArrears}
                where loan_code = @loan_code
                  and {NaturalKeyWhereClause()}
                """;
            await using var command = new SqlCommand(sql, connection);
            AddNaturalKeyParameters(command, loanCode, taxMemoDate, taxYear);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<bool> ExistsLoanMemoDateAndYearAsync(
            SqlConnection connection,
            string loanCode,
            DateTime? taxMemoDate,
            string? taxYear,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                select top (1) 1
                from {_tblTaxArrears}
                where loan_code = @loan_code
                  and {NaturalKeyWhereClause()}
                """;
            await using var command = new SqlCommand(sql, connection);
            AddNaturalKeyParameters(command, loanCode, taxMemoDate, taxYear);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        private static void AddNaturalKeyParameters(
            SqlCommand command,
            string loanCode,
            DateTime? taxMemoDate,
            string? taxYear)
        {
            command.Parameters.AddWithValue("@loan_code", loanCode.Trim());
            command.Parameters.AddWithValue(
                "@tax_memo_date",
                taxMemoDate.HasValue ? taxMemoDate.Value.Date : DBNull.Value);
            command.Parameters.AddWithValue("@tax_year", ToDbValue(NormalizeOptional(taxYear)));
        }

        private async Task<IReadOnlyList<TaxArrearsRowDto>> ReadRowsAsync(
            SqlCommand command,
            IReadOnlyList<int>? loanAliasIds,
            CancellationToken cancellationToken)
        {
            var rows = new List<TaxArrearsRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation(
                "Retrieved {Count} tax arrears rows (aliasFilter={AliasCount}).",
                rows.Count,
                loanAliasIds?.Count ?? 0);

            return rows;
        }

        private string BuildListSql(
            IReadOnlyList<int>? loanAliasIds,
            LoanStatusFilter statusFilter,
            string? loanStatusKeyColumn)
        {
            var needsStatusJoin = statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn);
            var keySelect = _hasTaxArrearKeyColumn
                ? "a.tax_arrear_key"
                : "tax_arrear_key = cast(0 as bigint)";

            var sql = new StringBuilder(
                $"""
                 select {keySelect},
                        loan_key = isnull(l.loan_key, 0),
                        a.loan_code,
                        b.loan_description,
                        b.loan_alias_name,
                        a.tax_memo_date,
                        a.tax_year,
                        a.tax_arrears,
                        a.tax_notes,
                        user_updated_by = {_auditColumns.BuildSelectUpdatedByExpression("a")},
                        user_updated_date = {_auditColumns.BuildSelectUpdatedDtmExpression("a")}
                 from {_tblTaxArrears} a
                 cross apply (
                     select top (1)
                            r.loan_code,
                            r.loan_description,
                            r.loan_alias_name
                     from {_tblLoanAliasRelationship} r
                     where r.loan_code = a.loan_code
                     order by r.loan_alias_name
                 ) b
                 left join {_tblDimLoan} l
                     on b.loan_code = l.loan_code
                 """);

            if (loanAliasIds is { Count: > 0 })
            {
                sql.AppendLine(
                    $"""
                     inner join {_tblLoanAliasMaster} m
                         on b.loan_alias_name = m.loan_alias_name
                     """);
            }

            if (loanAliasIds is { Count: > 0 })
            {
                sql.Append(" where m.loan_alias_id in (");
                sql.Append(string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}")));
                sql.Append(')');

                if (needsStatusJoin)
                {
                    LoanStatusFilterParser.AppendExistsSqlCondition(
                        sql,
                        "b",
                        _tblDimLoan,
                        loanStatusKeyColumn!,
                        statusFilter,
                        _tblDimStatus);
                }
            }
            else if (needsStatusJoin)
            {
                sql.AppendLine(" where 1 = 1");
                LoanStatusFilterParser.AppendExistsSqlCondition(
                    sql,
                    "b",
                    _tblDimLoan,
                    loanStatusKeyColumn!,
                    statusFilter,
                    _tblDimStatus);
            }

            sql.AppendLine();
            if (_hasTaxArrearKeyColumn)
            {
                sql.Append(" order by b.loan_alias_name, a.loan_code, a.tax_year, a.tax_memo_date, a.tax_arrear_key");
            }
            else
            {
                sql.Append(" order by b.loan_alias_name, a.loan_code, a.tax_year, a.tax_memo_date");
            }

            return sql.ToString();
        }

        private string BuildInsertSql()
        {
            var columns = new List<string> { "loan_code", "tax_memo_date", "tax_arrears", "tax_year", "tax_notes" };
            var values = new List<string> { "@loan_code", "@tax_memo_date", "@tax_arrears", "@tax_year", "@notes" };

            if (_hasTaxArrearKeyColumn)
            {
                columns.Insert(0, "tax_arrear_key");
                values.Insert(0, "@tax_arrear_key");
            }

            var auditInsert = _auditColumns.BuildInsertColumnList();
            columns.AddRange(auditInsert.Columns);
            values.AddRange(auditInsert.Values);

            return $"""
                insert into {_tblTaxArrears} ({string.Join(", ", columns)})
                values ({string.Join(", ", values)})
                """;
        }

        private string BuildUpdateByLoanCodeSql() =>
            $"""
                update {_tblTaxArrears}
                set tax_memo_date = @tax_memo_date,
                    tax_arrears = @tax_arrears,
                    tax_year = @tax_year,
                    tax_notes = @notes{_auditColumns.BuildUpdateSetClause()}
                where loan_code = @loan_code
                  and (
                        (@original_tax_memo_date is null and tax_memo_date is null)
                     or cast(tax_memo_date as date) = cast(@original_tax_memo_date as date)
                  )
                  and isnull(cast(tax_year as varchar(20)), '') = isnull(cast(@original_tax_year as varchar(20)), '')
                """;

        private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            if (_schemaProbed)
            {
                return;
            }

            await EnsureTableAvailableAsync(cancellationToken);

            _hasTaxArrearKeyColumn = await ColumnExistsAsync("tax_arrear_key", cancellationToken);
            _logger.LogInformation(
                "Tax arrears schema probe complete. table={Table}, hasTaxArrearKey={HasKey}",
                _tblTaxArrears,
                _hasTaxArrearKeyColumn);
            _auditColumns = await SubjectiveInputRelationshipAuditColumns.ProbeAsync(
                _connectionString,
                _tblTaxArrears,
                cancellationToken);
            _schemaProbed = true;
        }

        private async Task EnsureTableAvailableAsync(CancellationToken cancellationToken)
        {
            if (_tableAvailable == true)
            {
                return;
            }

            var probeSql = $"select top 0 loan_code from {_tblTaxArrears}";

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            try
            {
                await using var command = new SqlCommand(probeSql, connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                _tableAvailable = true;
            }
            catch (SqlException ex) when (ex.Number is 208 or 3701)
            {
                throw new InvalidOperationException(
                    "subjective_input.loan_tax_details does not exist. Verify wh_gold1 subjective_input schema.");
            }
        }

        private async Task<bool> ColumnExistsAsync(string columnName, CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand(
                    $"select top (0) [{columnName}] from {_tblTaxArrears}",
                    connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                return true;
            }
            catch (SqlException)
            {
                return false;
            }
        }

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
                    _tblDimLoan,
                    cancellationToken);
                return _loanStatusKeyColumn;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Tax arrears status filter skipped; shared.dim_loan status column unavailable.");
                return null;
            }
        }

        private async Task<string?> ResolveLoanCodeAsync(
            SqlConnection connection,
            TaxArrearsCreateRequest request,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.LoanCode))
            {
                return request.LoanCode.Trim();
            }

            if (request.LoanKey > 0)
            {
                return await GetLoanCodeByKeyAsync(connection, request.LoanKey, cancellationToken);
            }

            return null;
        }

        private async Task<string?> GetLoanCodeByKeyAsync(
            SqlConnection connection,
            long loanKey,
            CancellationToken cancellationToken)
        {
            var sql =
                $"select loan_code from {_tblDimLoan} l where l.loan_key = @loan_key";
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_key", loanKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? null : Convert.ToString(result);
        }

        private async Task<bool> IsLoanEligibleByCodeAsync(
            SqlConnection connection,
            string loanCode,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(_loanEligibleByCodeSql, connection);
            command.Parameters.AddWithValue("@loan_code", loanCode.Trim());
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        private async Task<long> GetNextTaxArrearKeyAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(_nextTaxArrearKeySql, connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }

        private async Task<TaxArrearsRowDto?> ReadByKeyAsync(
            SqlConnection connection,
            long taxArrearKey,
            CancellationToken cancellationToken)
        {
            var sql = BuildSelectSql("where a.tax_arrear_key = @tax_arrear_key");
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@tax_arrear_key", taxArrearKey);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
        }

        private async Task<TaxArrearsRowDto?> ReadByLoanMemoDateAndYearAsync(
            SqlConnection connection,
            string loanCode,
            DateTime? taxMemoDate,
            string? taxYear,
            CancellationToken cancellationToken)
        {
            var sql = BuildSelectSql(
                $"""
                where a.loan_code = @loan_code
                  and {NaturalKeyWhereClause("a")}
                """);
            await using var command = new SqlCommand(sql, connection);
            AddNaturalKeyParameters(command, loanCode, taxMemoDate, taxYear);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
        }

        private string BuildSelectSql(string whereClause)
        {
            var keySelect = _hasTaxArrearKeyColumn
                ? "a.tax_arrear_key"
                : "tax_arrear_key = cast(0 as bigint)";

            return $"""
                select top (1) {keySelect},
                       loan_key = isnull(l.loan_key, 0),
                       a.loan_code,
                       b.loan_description,
                       b.loan_alias_name,
                       a.tax_memo_date,
                       a.tax_year,
                       a.tax_arrears,
                       a.tax_notes,
                       user_updated_by = {_auditColumns.BuildSelectUpdatedByExpression("a")},
                       user_updated_date = {_auditColumns.BuildSelectUpdatedDtmExpression("a")}
                from {_tblTaxArrears} a
                cross apply (
                    select top (1)
                           r.loan_code,
                           r.loan_description,
                           r.loan_alias_name
                    from {_tblLoanAliasRelationship} r
                    where r.loan_code = a.loan_code
                    order by r.loan_alias_name
                ) b
                left join {_tblDimLoan} l
                    on b.loan_code = l.loan_code
                {whereClause}
                """;
        }

        private static void AddTaxArrearParameters(
            SqlCommand command,
            string loanCode,
            TaxArrearsCreateRequest request)
        {
            command.Parameters.AddWithValue("@loan_code", loanCode);
            command.Parameters.AddWithValue(
                "@tax_memo_date",
                request.TaxMemoDate.HasValue ? request.TaxMemoDate.Value.Date : DBNull.Value);
            command.Parameters.AddWithValue(
                "@tax_arrears",
                request.TaxArrears.HasValue ? request.TaxArrears.Value : DBNull.Value);
            command.Parameters.AddWithValue("@tax_year", ToDbValue(NormalizeOptional(request.TaxYear)));
            command.Parameters.AddWithValue("@notes", ToDbValue(NormalizeOptional(request.Notes)));
        }

        private static void AddLoanAliasParameters(SqlCommand command, IReadOnlyList<int> loanAliasIds)
        {
            for (var i = 0; i < loanAliasIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@loan_alias_id_{i}", loanAliasIds[i]);
            }
        }

        private static void ValidateCreateRequest(TaxArrearsCreateRequest request)
        {
            if (request.LoanKey <= 0 && string.IsNullOrWhiteSpace(request.LoanCode))
            {
                throw new InvalidOperationException("Loan key or loan code is required.");
            }

            if (!request.TaxMemoDate.HasValue)
            {
                throw new InvalidOperationException("Tax memo date is required.");
            }

            if (string.IsNullOrWhiteSpace(request.TaxYear))
            {
                throw new InvalidOperationException("Tax year is required.");
            }

            if (request.Notes is { Length: > 500 })
            {
                throw new InvalidOperationException("Notes must be 500 characters or fewer.");
            }
        }

        private static void ValidateUpdateItem(TaxArrearsUpdateItem item)
        {
            if (string.IsNullOrWhiteSpace(item.LoanCode))
            {
                throw new InvalidOperationException("Loan code is required.");
            }

            if (!item.OriginalTaxMemoDate.HasValue && !item.TaxMemoDate.HasValue)
            {
                throw new InvalidOperationException("Tax memo date is required.");
            }

            if (string.IsNullOrWhiteSpace(item.OriginalTaxYear) && string.IsNullOrWhiteSpace(item.TaxYear))
            {
                throw new InvalidOperationException("Tax year is required.");
            }

            if (item.Notes is { Length: > 500 })
            {
                throw new InvalidOperationException("Notes must be 500 characters or fewer.");
            }
        }

        private static TaxArrearsRowDto MapRow(SqlDataReader reader)
        {
            DateTime? updatedDate = null;
            if (reader.TryGetOrdinal("user_updated_date", out var dateOrd) && !reader.IsDBNull(dateOrd))
            {
                updatedDate = DateTime.SpecifyKind(reader.GetDateTime(dateOrd), DateTimeKind.Utc);
            }

            return new TaxArrearsRowDto
            {
                TaxArrearKey = GetInt64(reader, "tax_arrear_key"),
                LoanKey = GetInt64(reader, "loan_key"),
                LoanId = GetString(reader, "loan_code"),
                Description = GetString(reader, "loan_description"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                TaxMemoDate = GetNullableDate(reader, "tax_memo_date"),
                TaxArrears = GetNullableDecimal(reader, "tax_arrears"),
                TaxYear = GetNullableString(reader, "tax_year"),
                Notes = GetNullableString(reader, "tax_notes"),
                UserUpdatedBy = reader.TryGetOrdinal("user_updated_by", out var byOrd) && !reader.IsDBNull(byOrd)
                    ? reader.GetString(byOrd)
                    : null,
                UserUpdatedDate = updatedDate
            };
        }

        private static object ToDbValue(string? value) =>
            string.IsNullOrEmpty(value) ? DBNull.Value : value;

        private static string? NormalizeOptional(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

        private static decimal? GetNullableDecimal(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
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
