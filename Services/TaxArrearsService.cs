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
            IReadOnlyList<int> loanAliasIds,
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
        private readonly string _listSqlBase;
        private readonly string _nextTaxArrearKeySql;
        private readonly string _insertSql;
        private readonly string _selectByKeySql;
        private readonly string _updateSql;
        private readonly string _loanEligibleSql;

        private readonly string _connectionString;
        private readonly FabricWarehouseTables _tables;
        private readonly string _tblDimLoan;
        private readonly string _tblLoanAliasMaster;
        private readonly string _tblDimStatus;
        private readonly string _tblTaxArrears;
        private readonly ILogger<TaxArrearsService> _logger;
        private string? _loanStatusKeyColumn;
        private bool? _tableAvailable;

        public TaxArrearsService(
            IConfiguration configuration,
            ILogger<TaxArrearsService> logger,
            FabricWarehouseTables tables)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _tables = tables;
            var subjective = new SubjectiveInputSql(tables);
            _tblDimLoan = subjective.SharedDimLoan;
            _tblLoanAliasMaster = subjective.LoanAliasMaster;
            _tblDimStatus = subjective.DimStatus;
            _tblTaxArrears = subjective.LoanTaxDetails;
            var loanAliasRelationship = subjective.LoanAliasRelationship;

            _listSqlBase = $"""
                select a.tax_arrear_key,
                       {SubjectiveInputSql.LoanKeySelect(relationshipAlias: "b", dimLoanAlias: "l")},
                       a.loan_code,
                       b.loan_description,
                       b.loan_alias_name,
                       a.tax_memo_date,
                       a.tax_year,
                       a.tax_arrears,
                       a.tax_notes
                from {_tblTaxArrears} a
                inner join {loanAliasRelationship} b
                    on a.loan_code = b.loan_code
                left join {_tblLoanAliasMaster} m
                    on b.loan_alias_name = m.loan_alias_name
                {subjective.SharedDimLoanJoinOnLoanCode("b", "l")}
                where 1 = 1
                """;

            _nextTaxArrearKeySql = $"""
                select isnull(max(tax_arrear_key), 0) + 1
                from {_tblTaxArrears}
                """;

            _insertSql = $"""
                insert into {_tblTaxArrears} (
                    tax_arrear_key,
                    loan_code,
                    tax_memo_date,
                    tax_arrears,
                    tax_year,
                    tax_notes)
                values (
                    @tax_arrear_key,
                    @loan_code,
                    @tax_memo_date,
                    @tax_arrears,
                    @tax_year,
                    @notes)
                """;

            _selectByKeySql = $"""
                select a.tax_arrear_key,
                       {SubjectiveInputSql.LoanKeySelect(relationshipAlias: "b", dimLoanAlias: "l")},
                       a.loan_code,
                       b.loan_description,
                       b.loan_alias_name,
                       a.tax_memo_date,
                       a.tax_year,
                       a.tax_arrears,
                       a.tax_notes
                from {_tblTaxArrears} a
                inner join {loanAliasRelationship} b
                    on a.loan_code = b.loan_code
                left join {_tblLoanAliasMaster} m
                    on b.loan_alias_name = m.loan_alias_name
                {subjective.SharedDimLoanJoinOnLoanCode("b", "l")}
                where a.tax_arrear_key = @tax_arrear_key
                """;

            _updateSql = $"""
                update {_tblTaxArrears}
                set tax_memo_date = @tax_memo_date,
                    tax_arrears = @tax_arrears,
                    tax_year = @tax_year,
                    tax_notes = @notes
                where tax_arrear_key = @tax_arrear_key
                """;

            _loanEligibleSql = $"""
                select r.loan_code
                from {loanAliasRelationship} r
                inner join {_tblDimLoan} l
                    on {SubjectiveInputSql.EqualsVarchar("l", "loan_code", "r", "loan_code")}
                   and l.loan_key = @loan_key
                   and {SubjectiveInputSql.DimLoanIsCurrent("l")}
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
            IReadOnlyList<int> loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default)
        {
            await EnsureTableAvailableAsync(cancellationToken);

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

            var rows = new List<TaxArrearsRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation(
                "Retrieved {Count} tax arrears rows for {AliasCount} loan alias filter(s).",
                rows.Count,
                loanAliasIds.Count);

            return rows;
        }

        public async Task<TaxArrearsRowDto> CreateAsync(
            TaxArrearsCreateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            ValidateCreateRequest(request);
            await EnsureTableAvailableAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            if (!await IsLoanEligibleAsync(connection, request.LoanKey, cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Loan {request.LoanKey} is not eligible (must be current and leaf; is_leaf may be unset until ETL).");
            }

            var loanCode = await GetLoanCodeAsync(connection, request.LoanKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(loanCode))
            {
                throw new InvalidOperationException($"Loan {request.LoanKey} could not be resolved to loan_code.");
            }

            var taxArrearKey = await GetNextTaxArrearKeyAsync(connection, cancellationToken);

            await using (var insertCommand = new SqlCommand(_insertSql, connection))
            {
                AddTaxArrearParameters(insertCommand, taxArrearKey, loanCode, request, auditDisplayName);
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            var row = await ReadByKeyAsync(connection, taxArrearKey, cancellationToken);
            if (row is null)
            {
                throw new InvalidOperationException("Tax arrears record was created but could not be read back.");
            }

            _logger.LogInformation("Created tax arrears record {TaxArrearKey} for loan {LoanKey}.", taxArrearKey, request.LoanKey);
            return row;
        }

        public async Task<bool> UpdateAsync(
            TaxArrearsBulkUpdateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await EnsureTableAvailableAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var item in request.TaxArrears)
            {
                ValidateUpdateItem(item);

                await using var command = new SqlCommand(_updateSql, connection);
                command.Parameters.AddWithValue("@tax_arrear_key", item.TaxArrearKey);
                command.Parameters.AddWithValue(
                    "@tax_memo_date",
                    item.TaxMemoDate.HasValue ? item.TaxMemoDate.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@tax_arrears",
                    item.TaxArrears.HasValue ? item.TaxArrears.Value : DBNull.Value);
                command.Parameters.AddWithValue("@tax_year", ToDbValue(NormalizeOptional(item.TaxYear)));
                command.Parameters.AddWithValue("@notes", ToDbValue(NormalizeOptional(item.Notes)));

                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Updated {AffectedRows} tax arrears rows.", affectedRows);
                return true;
            }

            _logger.LogWarning("No tax arrears rows updated.");
            return false;
        }

        private string BuildListSql(
            IReadOnlyList<int> loanAliasIds,
            LoanStatusFilter statusFilter,
            string? loanStatusKeyColumn)
        {
            var sql = new StringBuilder(_listSqlBase);

            sql.Append(" and m.loan_alias_id in (");
            sql.Append(string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}")));
            sql.Append(')');

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendSqlCondition(sql, "l", loanStatusKeyColumn, statusFilter, _tblDimStatus);
            }

            sql.AppendLine();
            sql.Append(" order by b.loan_alias_name, a.loan_code, a.tax_year, a.tax_memo_date, a.tax_arrear_key");
            return sql.ToString();
        }

        private async Task EnsureTableAvailableAsync(CancellationToken cancellationToken)
        {
            if (_tableAvailable == true)
            {
                return;
            }

            var probeSql = $"select top 0 tax_arrear_key from {_tblTaxArrears}";

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

        private async Task<string?> GetLoanCodeAsync(
            SqlConnection connection,
            long loanKey,
            CancellationToken cancellationToken)
        {
            var sql = $"select loan_code from {_tblDimLoan} where loan_key = @loan_key and cast(scd_cur_ind as varchar(10)) in ('1', 'Y')";
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_key", loanKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? null : Convert.ToString(result);
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
            await using var command = new SqlCommand(_selectByKeySql, connection);
            command.Parameters.AddWithValue("@tax_arrear_key", taxArrearKey);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
        }

        private static void AddTaxArrearParameters(
            SqlCommand command,
            long taxArrearKey,
            string loanCode,
            TaxArrearsCreateRequest request,
            string auditDisplayName)
        {
            command.Parameters.AddWithValue("@tax_arrear_key", taxArrearKey);
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
            if (request.LoanKey <= 0)
            {
                throw new InvalidOperationException("Loan key is required.");
            }

            if (request.Notes is { Length: > 500 })
            {
                throw new InvalidOperationException("Notes must be 500 characters or fewer.");
            }
        }

        private static void ValidateUpdateItem(TaxArrearsUpdateItem item)
        {
            if (item.TaxArrearKey <= 0)
            {
                throw new InvalidOperationException("Tax arrear key is required.");
            }

            if (item.Notes is { Length: > 500 })
            {
                throw new InvalidOperationException("Notes must be 500 characters or fewer.");
            }
        }

        private static TaxArrearsRowDto MapRow(SqlDataReader reader) =>
            new()
            {
                TaxArrearKey = GetInt64(reader, "tax_arrear_key"),
                LoanKey = GetInt64(reader, "loan_key"),
                LoanId = GetString(reader, "loan_code"),
                Description = GetString(reader, "loan_description"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                TaxMemoDate = GetNullableDate(reader, "tax_memo_date"),
                TaxArrears = GetNullableDecimal(reader, "tax_arrears"),
                TaxYear = GetNullableString(reader, "tax_year"),
                Notes = GetNullableString(reader, "tax_notes")
            };

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

        private static DateTime? GetNullableDateTime(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
    }
}
