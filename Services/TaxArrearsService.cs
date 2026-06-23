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
        private const string ListSqlBase = """
            select ta.tax_arrear_key,
                   l.loan_key,
                   l.loan_code,
                   l.loan_desc,
                   loan_alias_name = isnull(m.loan_alias_name, ''),
                   ta.tax_memo_date,
                   ta.tax_arrears,
                   ta.tax_year,
                   ta.notes,
                   ta.user_updated_by,
                   ta.user_updated_date
            from mort.tax_arrears ta
            inner join mort.dim_loan l
                on ta.loan_key = l.loan_key
            left join mort.loan_alias_master m
                on l.loan_alias_key = m.loan_alias_id
            where l.is_current = 1
              and (l.is_leaf = 1 or l.is_leaf is null)
            """;

        private const string NextTaxArrearKeySql = """
            select isnull(max(tax_arrear_key), 0) + 1
            from mort.tax_arrears
            """;

        private const string InsertSql = """
            insert into mort.tax_arrears (
                tax_arrear_key,
                loan_key,
                tax_memo_date,
                tax_arrears,
                tax_year,
                notes,
                user_updated_by,
                user_updated_date)
            values (
                @tax_arrear_key,
                @loan_key,
                @tax_memo_date,
                @tax_arrears,
                @tax_year,
                @notes,
                @user_updated_by,
                sysutcdatetime())
            """;

        private const string SelectByKeySql = """
            select ta.tax_arrear_key,
                   l.loan_key,
                   l.loan_code,
                   l.loan_desc,
                   loan_alias_name = isnull(m.loan_alias_name, ''),
                   ta.tax_memo_date,
                   ta.tax_arrears,
                   ta.tax_year,
                   ta.notes,
                   ta.user_updated_by,
                   ta.user_updated_date
            from mort.tax_arrears ta
            inner join mort.dim_loan l
                on ta.loan_key = l.loan_key
            left join mort.loan_alias_master m
                on l.loan_alias_key = m.loan_alias_id
            where ta.tax_arrear_key = @tax_arrear_key
            """;

        private const string UpdateSql = """
            update mort.tax_arrears
            set tax_memo_date = @tax_memo_date,
                tax_arrears = @tax_arrears,
                tax_year = @tax_year,
                notes = @notes,
                user_updated_by = @user_updated_by,
                user_updated_date = sysutcdatetime()
            where tax_arrear_key = @tax_arrear_key
            """;

        private const string LoanEligibleSql = """
            select 1
            from mort.dim_loan
            where loan_key = @loan_key
              and is_current = 1
              and (is_leaf = 1 or is_leaf is null)
            """;

        private readonly string _connectionString;
        private readonly ILogger<TaxArrearsService> _logger;
        private string? _loanStatusKeyColumn;
        private bool? _tableAvailable;

        public TaxArrearsService(IConfiguration configuration, ILogger<TaxArrearsService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
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
                    throw new InvalidOperationException("Status filter requires loan_status_key on mort.dim_loan.");
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

            var taxArrearKey = await GetNextTaxArrearKeyAsync(connection, cancellationToken);

            await using (var insertCommand = new SqlCommand(InsertSql, connection))
            {
                AddTaxArrearParameters(insertCommand, taxArrearKey, request, auditDisplayName);
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

                await using var command = new SqlCommand(UpdateSql, connection);
                command.Parameters.AddWithValue("@tax_arrear_key", item.TaxArrearKey);
                command.Parameters.AddWithValue(
                    "@tax_memo_date",
                    item.TaxMemoDate.HasValue ? item.TaxMemoDate.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@tax_arrears",
                    item.TaxArrears.HasValue ? item.TaxArrears.Value : DBNull.Value);
                command.Parameters.AddWithValue("@tax_year", ToDbValue(NormalizeOptional(item.TaxYear)));
                command.Parameters.AddWithValue("@notes", ToDbValue(NormalizeOptional(item.Notes)));
                command.Parameters.AddWithValue("@user_updated_by", auditDisplayName);

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

        private static string BuildListSql(
            IReadOnlyList<int> loanAliasIds,
            LoanStatusFilter statusFilter,
            string? loanStatusKeyColumn)
        {
            var sql = new StringBuilder(ListSqlBase);

            sql.Append(" and l.loan_alias_key in (");
            sql.Append(string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}")));
            sql.Append(')');

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendSqlCondition(sql, "l", loanStatusKeyColumn, statusFilter);
            }

            sql.AppendLine();
            sql.Append(" order by m.loan_alias_name, l.loan_code, ta.tax_year, ta.tax_memo_date, ta.tax_arrear_key");
            return sql.ToString();
        }

        private async Task EnsureTableAvailableAsync(CancellationToken cancellationToken)
        {
            if (_tableAvailable == true)
            {
                return;
            }

            const string probeSql = "select top 0 tax_arrear_key from mort.tax_arrears";

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
                    "mort.tax_arrears does not exist. Run Scripts/Create_mort_tax_arrears.sql.");
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
                cancellationToken);

            return _loanStatusKeyColumn;
        }

        private static async Task<bool> IsLoanEligibleAsync(
            SqlConnection connection,
            long loanKey,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(LoanEligibleSql, connection);
            command.Parameters.AddWithValue("@loan_key", loanKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        private static async Task<long> GetNextTaxArrearKeyAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(NextTaxArrearKeySql, connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }

        private static async Task<TaxArrearsRowDto?> ReadByKeyAsync(
            SqlConnection connection,
            long taxArrearKey,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(SelectByKeySql, connection);
            command.Parameters.AddWithValue("@tax_arrear_key", taxArrearKey);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
        }

        private static void AddTaxArrearParameters(
            SqlCommand command,
            long taxArrearKey,
            TaxArrearsCreateRequest request,
            string auditDisplayName)
        {
            command.Parameters.AddWithValue("@tax_arrear_key", taxArrearKey);
            command.Parameters.AddWithValue("@loan_key", request.LoanKey);
            command.Parameters.AddWithValue(
                "@tax_memo_date",
                request.TaxMemoDate.HasValue ? request.TaxMemoDate.Value.Date : DBNull.Value);
            command.Parameters.AddWithValue(
                "@tax_arrears",
                request.TaxArrears.HasValue ? request.TaxArrears.Value : DBNull.Value);
            command.Parameters.AddWithValue("@tax_year", ToDbValue(NormalizeOptional(request.TaxYear)));
            command.Parameters.AddWithValue("@notes", ToDbValue(NormalizeOptional(request.Notes)));
            command.Parameters.AddWithValue("@user_updated_by", auditDisplayName);
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
                Description = GetString(reader, "loan_desc"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                TaxMemoDate = GetNullableDate(reader, "tax_memo_date"),
                TaxArrears = GetNullableDecimal(reader, "tax_arrears"),
                TaxYear = GetNullableString(reader, "tax_year"),
                Notes = GetNullableString(reader, "notes"),
                UserUpdatedBy = GetNullableString(reader, "user_updated_by"),
                UserUpdatedDate = GetNullableDateTime(reader, "user_updated_date")
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
