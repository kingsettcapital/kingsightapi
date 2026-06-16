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
            CancellationToken cancellationToken = default);

        Task<bool> ConfirmAsync(
            LtvValidationConfirmRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed class LtvValidationService : ILtvValidationService
    {
        private static readonly string[] ParentLoanKeyColumnCandidates =
            ["parent_loan_key", "loan_parent_key", "parent_key"];

        private static readonly string[] ExposureColumnCandidates =
            ["exposure", "loan_exposure", "outstanding_balance", "collateral"];

        private static readonly string[] DimLoanLtvColumnCandidates =
            ["ai_ltv", "loan_ltv", "ltv", "ltv_percent"];

        private const string ListSqlFrom = """
            from mort.dim_loan l
            left join mort.loan_alias_master m
                on l.loan_alias_key = m.loan_alias_id
            left join mort.dim_investor inv
                on l.investor_key = inv.investor_key
               and inv.is_current = 1
            left join mort.investor_alias_master iam
                on inv.investor_alias_key = iam.investor_alias_id
            left join mort.ltv_validation lv
                on l.loan_key = lv.loan_key
            where l.is_current = 1
              and (l.is_leaf = 1 or l.is_leaf is null)
            """;

        private const string UpsertOverrideSql = """
            update mort.ltv_validation
            set ltv = @ltv,
                is_user_overridden = 1,
                is_ai_confirmed = 0,
                user_updated_by = @user_updated_by,
                user_updated_date = sysutcdatetime()
            where loan_key = @loan_key
            """;

        private const string InsertOverrideSql = """
            insert into mort.ltv_validation (
                loan_key,
                ai_ltv,
                ltv,
                ai_commentary,
                is_ai_confirmed,
                is_user_overridden,
                user_updated_by,
                user_updated_date)
            values (
                @loan_key,
                null,
                @ltv,
                null,
                0,
                1,
                @user_updated_by,
                sysutcdatetime())
            """;

        private const string ConfirmAiLtvSql = """
            update mort.ltv_validation
            set ltv = ai_ltv,
                is_ai_confirmed = 1,
                is_user_overridden = 0,
                user_updated_by = @user_updated_by,
                user_updated_date = sysutcdatetime()
            where loan_key = @loan_key
              and ai_ltv is not null
            """;

        private const string ConfirmPendingLtvSql = """
            update mort.ltv_validation
            set ai_ltv = coalesce(ai_ltv, ltv),
                ltv = coalesce(ltv, ai_ltv),
                is_ai_confirmed = 1,
                is_user_overridden = 0,
                user_updated_by = @user_updated_by,
                user_updated_date = sysutcdatetime()
            where loan_key = @loan_key
              and ltv is not null
              and (is_user_overridden is null or is_user_overridden = 0)
            """;

        private const string InsertConfirmedSql = """
            insert into mort.ltv_validation (
                loan_key,
                ai_ltv,
                ltv,
                ai_commentary,
                is_ai_confirmed,
                is_user_overridden,
                user_updated_by,
                user_updated_date)
            values (
                @loan_key,
                @ai_ltv,
                @ai_ltv,
                null,
                1,
                0,
                @user_updated_by,
                sysutcdatetime())
            """;

        private const string ReadValidationStateSql = """
            select ai_ltv,
                   ltv,
                   is_user_overridden
            from mort.ltv_validation
            where loan_key = @loan_key
            """;

        private const string LoanEligibleSql = """
            select 1
            from mort.dim_loan
            where loan_key = @loan_key
              and is_current = 1
              and (is_leaf = 1 or is_leaf is null)
            """;

        private readonly string _connectionString;
        private readonly ILogger<LtvValidationService> _logger;
        private string? _loanStatusKeyColumn;
        private string? _parentLoanKeyColumn;
        private bool? _parentLoanKeyColumnResolved;
        private string? _exposureColumn;
        private bool? _exposureColumnResolved;
        private string? _dimLoanLtvColumn;
        private bool? _dimLoanLtvColumnResolved;
        private bool? _ltvTableAvailable;

        public LtvValidationService(IConfiguration configuration, ILogger<LtvValidationService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
        }

        public async Task<IReadOnlyList<LtvValidationRowDto>> GetAsync(
            IReadOnlyList<int> loanAliasIds,
            IReadOnlyList<string>? statuses,
            CancellationToken cancellationToken = default)
        {
            await EnsureLtvTableAvailableAsync(cancellationToken);
            var parentLoanKeyColumn = await GetParentLoanKeyColumnAsync(cancellationToken);
            var exposureColumn = await GetExposureColumnAsync(cancellationToken);
            var dimLoanLtvColumn = await GetDimLoanLtvColumnAsync(cancellationToken);

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
                parentLoanKeyColumn,
                exposureColumn,
                dimLoanLtvColumn);

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
            CancellationToken cancellationToken = default)
        {
            await EnsureLtvTableAvailableAsync(cancellationToken);

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

                if (string.IsNullOrWhiteSpace(loan.UserUpdatedBy))
                {
                    throw new InvalidOperationException("User updated by is required.");
                }

                if (!await IsLoanEligibleAsync(connection, loan.LoanKey, cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"Loan {loan.LoanKey} is not eligible (must be current and leaf).");
                }

                await using var updateCommand = new SqlCommand(UpsertOverrideSql, connection);
                updateCommand.Parameters.AddWithValue("@loan_key", loan.LoanKey);
                updateCommand.Parameters.AddWithValue(
                    "@ltv",
                    loan.Ltv.HasValue ? loan.Ltv.Value : DBNull.Value);
                updateCommand.Parameters.AddWithValue("@user_updated_by", loan.UserUpdatedBy);

                var updated = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
                if (updated == 0)
                {
                    await using var insertCommand = new SqlCommand(InsertOverrideSql, connection);
                    insertCommand.Parameters.AddWithValue("@loan_key", loan.LoanKey);
                    insertCommand.Parameters.AddWithValue(
                        "@ltv",
                        loan.Ltv.HasValue ? loan.Ltv.Value : DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@user_updated_by", loan.UserUpdatedBy);
                    updated = await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                affectedRows += updated;
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
            CancellationToken cancellationToken = default)
        {
            if (request.LoanKeys.Count == 0)
            {
                throw new InvalidOperationException("At least one loan key is required.");
            }

            if (string.IsNullOrWhiteSpace(request.UserUpdatedBy))
            {
                throw new InvalidOperationException("User updated by is required.");
            }

            await EnsureLtvTableAvailableAsync(cancellationToken);
            var dimLoanLtvColumn = await GetDimLoanLtvColumnAsync(cancellationToken);

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
                        $"Loan {loanKey} is not eligible (must be current and leaf).");
                }

                affectedRows += await ConfirmLoanAsync(
                    connection,
                    loanKey,
                    request.UserUpdatedBy,
                    dimLoanLtvColumn,
                    cancellationToken);
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Confirmed AI LTV for {AffectedRows} loan row(s).", affectedRows);
                return true;
            }

            return false;
        }

        private async Task<int> ConfirmLoanAsync(
            SqlConnection connection,
            long loanKey,
            string userUpdatedBy,
            string? dimLoanLtvColumn,
            CancellationToken cancellationToken)
        {
            var state = await ReadValidationStateAsync(connection, loanKey, cancellationToken);
            if (state is not null)
            {
                if (state.AiLtv.HasValue)
                {
                    return await ExecuteConfirmCommandAsync(
                        connection,
                        ConfirmAiLtvSql,
                        loanKey,
                        userUpdatedBy,
                        cancellationToken);
                }

                if (state.Ltv.HasValue && state.IsUserOverridden != true)
                {
                    return await ExecuteConfirmCommandAsync(
                        connection,
                        ConfirmPendingLtvSql,
                        loanKey,
                        userUpdatedBy,
                        cancellationToken);
                }

                if (state.Ltv.HasValue && state.IsUserOverridden == true)
                {
                    throw new InvalidOperationException(
                        $"Loan {loanKey} has a manual LTV override. Use Save Changes instead of Confirm AI LTV.");
                }
            }

            var dimLoanLtv = await ReadDimLoanLtvAsync(
                connection,
                loanKey,
                dimLoanLtvColumn,
                cancellationToken);

            if (dimLoanLtv.HasValue)
            {
                await using var insertCommand = new SqlCommand(InsertConfirmedSql, connection);
                insertCommand.Parameters.AddWithValue("@loan_key", loanKey);
                insertCommand.Parameters.AddWithValue("@ai_ltv", dimLoanLtv.Value);
                insertCommand.Parameters.AddWithValue("@user_updated_by", userUpdatedBy);
                return await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            throw new InvalidOperationException(
                $"Loan {loanKey} has no AI LTV to confirm. Import AI data into mort.ltv_validation or dim_loan first.");
        }

        private static async Task<int> ExecuteConfirmCommandAsync(
            SqlConnection connection,
            string sql,
            long loanKey,
            string userUpdatedBy,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_key", loanKey);
            command.Parameters.AddWithValue("@user_updated_by", userUpdatedBy);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<ValidationState?> ReadValidationStateAsync(
            SqlConnection connection,
            long loanKey,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(ReadValidationStateSql, connection);
            command.Parameters.AddWithValue("@loan_key", loanKey);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ValidationState
            {
                AiLtv = GetNullableDecimal(reader, "ai_ltv"),
                Ltv = GetNullableDecimal(reader, "ltv"),
                IsUserOverridden = GetNullableBoolean(reader, "is_user_overridden")
            };
        }

        private static async Task<decimal?> ReadDimLoanLtvAsync(
            SqlConnection connection,
            long loanKey,
            string? dimLoanLtvColumn,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(dimLoanLtvColumn))
            {
                return null;
            }

            var sql = $"select [{dimLoanLtvColumn}] from mort.dim_loan where loan_key = @loan_key and is_current = 1";
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_key", loanKey);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull)
            {
                return null;
            }

            var value = Convert.ToDecimal(result);
            ValidateLtv(value);
            return value;
        }

        private string BuildListSql(
            IReadOnlyList<int> loanAliasIds,
            LoanStatusFilter statusFilter,
            string? loanStatusKeyColumn,
            string? parentLoanKeyColumn,
            string? exposureColumn,
            string? dimLoanLtvColumn)
        {
            var parentLoanIdSelect = string.IsNullOrEmpty(parentLoanKeyColumn)
                ? "parent_loan_id = isnull(l.dummy_loan_link, '')"
                : "parent_loan_id = isnull(parent.loan_code, isnull(l.dummy_loan_link, ''))";

            var exposureSelect = string.IsNullOrEmpty(exposureColumn)
                ? "cast(null as decimal(18, 2)) as exposure"
                : $"l.{exposureColumn} as exposure";

            var sql = new StringBuilder();
            sql.AppendLine("select l.loan_key,");
            sql.AppendLine($"       {parentLoanIdSelect},");
            sql.AppendLine("""
                       child_loan_id = l.loan_code,
                       l.loan_desc,
                       loan_alias_name = isnull(m.loan_alias_name, ''),
                       investor_alias_name = isnull(iam.investor_alias_name, ''),
                       m.security_value,
                """);
            sql.AppendLine($"       {exposureSelect},");
            var ltvSelect = string.IsNullOrEmpty(dimLoanLtvColumn)
                ? "ltv = coalesce(lv.ltv, lv.ai_ltv)"
                : $"ltv = coalesce(lv.ltv, lv.ai_ltv, l.{dimLoanLtvColumn})";

            sql.AppendLine("       l.loan_ranking as ranking,");
            sql.AppendLine($"       {ltvSelect},");
            sql.AppendLine("""
                       lv.ai_commentary,
                       user_updated_by = coalesce(lv.user_updated_by, l.user_updated_by),
                       user_updated_date = coalesce(lv.user_updated_date, l.user_updated_date)
                """);

            if (!string.IsNullOrEmpty(parentLoanKeyColumn))
            {
                sql.AppendLine("""
                    from mort.dim_loan l
                    left join mort.dim_loan parent
                """);
                sql.AppendLine($"       on l.{parentLoanKeyColumn} = parent.loan_key");
                sql.AppendLine("""
                          and parent.is_current = 1
                    left join mort.loan_alias_master m
                        on l.loan_alias_key = m.loan_alias_id
                    left join mort.dim_investor inv
                        on l.investor_key = inv.investor_key
                       and inv.is_current = 1
                    left join mort.investor_alias_master iam
                        on inv.investor_alias_key = iam.investor_alias_id
                    left join mort.ltv_validation lv
                        on l.loan_key = lv.loan_key
                    where l.is_current = 1
                      and (l.is_leaf = 1 or l.is_leaf is null)
                    """);
            }
            else
            {
                sql.Append(ListSqlFrom);
            }

            sql.Append(" and l.loan_alias_key in (");
            sql.Append(string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}")));
            sql.Append(')');

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendSqlCondition(sql, "l", loanStatusKeyColumn, statusFilter);
            }

            sql.AppendLine();
            sql.Append(" order by m.loan_alias_name, l.loan_code");
            return sql.ToString();
        }

        private async Task EnsureLtvTableAvailableAsync(CancellationToken cancellationToken)
        {
            if (_ltvTableAvailable == true)
            {
                return;
            }

            const string probeSql = "select top 0 loan_key from mort.ltv_validation";

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            try
            {
                await using var command = new SqlCommand(probeSql, connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                _ltvTableAvailable = true;
            }
            catch (SqlException ex) when (ex.Number is 208 or 3701)
            {
                throw new InvalidOperationException(
                    "mort.ltv_validation does not exist. Run Scripts/Create_mort_ltv_validation.sql.");
            }
        }

        private async Task<string?> GetParentLoanKeyColumnAsync(CancellationToken cancellationToken)
        {
            if (_parentLoanKeyColumnResolved == true)
            {
                return _parentLoanKeyColumn;
            }

            _parentLoanKeyColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                ParentLoanKeyColumnCandidates,
                cancellationToken);

            _parentLoanKeyColumnResolved = true;
            if (!string.IsNullOrEmpty(_parentLoanKeyColumn))
            {
                _logger.LogInformation(
                    "Using mort.dim_loan.{Column} for LTV validation parent loan join.",
                    _parentLoanKeyColumn);
            }

            return _parentLoanKeyColumn;
        }

        private async Task<string?> GetExposureColumnAsync(CancellationToken cancellationToken)
        {
            if (_exposureColumnResolved == true)
            {
                return _exposureColumn;
            }

            _exposureColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                ExposureColumnCandidates,
                cancellationToken);

            _exposureColumnResolved = true;
            if (!string.IsNullOrEmpty(_exposureColumn))
            {
                _logger.LogInformation(
                    "Using mort.dim_loan.{Column} for LTV validation exposure.",
                    _exposureColumn);
            }

            return _exposureColumn;
        }

        private async Task<string?> GetDimLoanLtvColumnAsync(CancellationToken cancellationToken)
        {
            if (_dimLoanLtvColumnResolved == true)
            {
                return _dimLoanLtvColumn;
            }

            _dimLoanLtvColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                DimLoanLtvColumnCandidates,
                cancellationToken);

            _dimLoanLtvColumnResolved = true;
            if (!string.IsNullOrEmpty(_dimLoanLtvColumn))
            {
                _logger.LogInformation(
                    "Using mort.dim_loan.{Column} for LTV validation fallback LTV.",
                    _dimLoanLtvColumn);
            }

            return _dimLoanLtvColumn;
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
                AiCommentary = GetNullableString(reader, "ai_commentary"),
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

        private static bool? GetNullableBoolean(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToBoolean(reader.GetValue(ordinal));
        }

        private sealed class ValidationState
        {
            public decimal? AiLtv { get; init; }
            public decimal? Ltv { get; init; }
            public bool? IsUserOverridden { get; init; }
        }
    }
}
