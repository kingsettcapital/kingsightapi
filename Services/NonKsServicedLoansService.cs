using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface INonKsServicedLoansService
    {
        Task<IReadOnlyList<NonKsServicedLoanRowDto>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<NonKsServicedLoanRowDto>> CreateAsync(
            NonKsServicedLoanBulkCreateRequest request,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            NonKsServicedLoanBulkUpdateRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed class NonKsServicedLoansService : INonKsServicedLoansService
    {
        private const string ListSql = """
            select non_ks_serviced_loan_key,
                   loan_name,
                   as_at_date,
                   loan_id,
                   servicer_id,
                   description,
                   investor,
                   date_of_default,
                   maturity_date,
                   interest_off_date,
                   tax_memo_date,
                   security_value,
                   units,
                   net_acres,
                   square_feet,
                   interest_rate,
                   principal_balance,
                   outstanding_interest,
                   accrued_interest,
                   late_interest,
                   outstanding_invoices,
                   est_realization_costs,
                   cost_to_complete,
                   tax_arrears,
                   interest_as_of_tax_memo,
                   interest_adjustment,
                   user_updated_by,
                   user_updated_date
            from mort.non_ks_serviced_loan
            order by loan_name, as_at_date, non_ks_serviced_loan_key
            """;

        private const string SelectByKeySql = """
            select non_ks_serviced_loan_key,
                   loan_name,
                   as_at_date,
                   loan_id,
                   servicer_id,
                   description,
                   investor,
                   date_of_default,
                   maturity_date,
                   interest_off_date,
                   tax_memo_date,
                   security_value,
                   units,
                   net_acres,
                   square_feet,
                   interest_rate,
                   principal_balance,
                   outstanding_interest,
                   accrued_interest,
                   late_interest,
                   outstanding_invoices,
                   est_realization_costs,
                   cost_to_complete,
                   tax_arrears,
                   interest_as_of_tax_memo,
                   interest_adjustment,
                   user_updated_by,
                   user_updated_date
            from mort.non_ks_serviced_loan
            where non_ks_serviced_loan_key = @non_ks_serviced_loan_key
            """;

        private const string NextKeySql = """
            select isnull(max(non_ks_serviced_loan_key), 0) + 1
            from mort.non_ks_serviced_loan
            """;

        private const string LoanIdExistsSql = """
            select 1
            from mort.non_ks_serviced_loan
            where loan_id = @loan_id
            """;

        private const string InsertSql = """
            insert into mort.non_ks_serviced_loan (
                non_ks_serviced_loan_key,
                loan_name,
                as_at_date,
                loan_id,
                servicer_id,
                description,
                investor,
                date_of_default,
                maturity_date,
                interest_off_date,
                tax_memo_date,
                security_value,
                units,
                net_acres,
                square_feet,
                interest_rate,
                principal_balance,
                outstanding_interest,
                accrued_interest,
                late_interest,
                outstanding_invoices,
                est_realization_costs,
                cost_to_complete,
                tax_arrears,
                interest_as_of_tax_memo,
                interest_adjustment,
                user_updated_by,
                user_updated_date)
            values (
                @non_ks_serviced_loan_key,
                @loan_name,
                @as_at_date,
                @loan_id,
                @servicer_id,
                @description,
                @investor,
                @date_of_default,
                @maturity_date,
                @interest_off_date,
                @tax_memo_date,
                @security_value,
                @units,
                @net_acres,
                @square_feet,
                @interest_rate,
                @principal_balance,
                @outstanding_interest,
                @accrued_interest,
                @late_interest,
                @outstanding_invoices,
                @est_realization_costs,
                @cost_to_complete,
                @tax_arrears,
                @interest_as_of_tax_memo,
                @interest_adjustment,
                @user_updated_by,
                sysutcdatetime())
            """;

        private const string UpdateSql = """
            update mort.non_ks_serviced_loan
            set loan_name = @loan_name,
                as_at_date = @as_at_date,
                loan_id = @loan_id,
                servicer_id = @servicer_id,
                description = @description,
                investor = @investor,
                date_of_default = @date_of_default,
                maturity_date = @maturity_date,
                interest_off_date = @interest_off_date,
                tax_memo_date = @tax_memo_date,
                security_value = @security_value,
                units = @units,
                net_acres = @net_acres,
                square_feet = @square_feet,
                interest_rate = @interest_rate,
                principal_balance = @principal_balance,
                outstanding_interest = @outstanding_interest,
                accrued_interest = @accrued_interest,
                late_interest = @late_interest,
                outstanding_invoices = @outstanding_invoices,
                est_realization_costs = @est_realization_costs,
                cost_to_complete = @cost_to_complete,
                tax_arrears = @tax_arrears,
                interest_as_of_tax_memo = @interest_as_of_tax_memo,
                interest_adjustment = @interest_adjustment,
                user_updated_by = @user_updated_by,
                user_updated_date = sysutcdatetime()
            where non_ks_serviced_loan_key = @non_ks_serviced_loan_key
            """;

        private readonly string _connectionString;
        private readonly ILogger<NonKsServicedLoansService> _logger;
        private bool? _tableAvailable;

        public NonKsServicedLoansService(IConfiguration configuration, ILogger<NonKsServicedLoansService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
        }

        public async Task<IReadOnlyList<NonKsServicedLoanRowDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            await EnsureTableAvailableAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(ListSql, connection);
            var rows = new List<NonKsServicedLoanRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation("Retrieved {Count} non-KS serviced loan rows.", rows.Count);
            return rows;
        }

        public async Task<IReadOnlyList<NonKsServicedLoanRowDto>> CreateAsync(
            NonKsServicedLoanBulkCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Loans.Count == 0)
            {
                throw new InvalidOperationException("Request body must include at least one loan row.");
            }

            await EnsureTableAvailableAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var created = new List<NonKsServicedLoanRowDto>();
            foreach (var loan in request.Loans)
            {
                var validationError = NonKsServicedLoansValidation.ValidateCreateItem(loan);
                if (validationError is not null)
                {
                    throw new InvalidOperationException(validationError);
                }

                var loanId = NormalizeOptional(loan.LoanId);
                if (!string.IsNullOrEmpty(loanId)
                    && await LoanIdExistsAsync(connection, loanId, cancellationToken))
                {
                    throw new InvalidOperationException($"Loan ID '{loanId}' already exists.");
                }

                var key = await GetNextKeyAsync(connection, cancellationToken);
                if (string.IsNullOrEmpty(loanId))
                {
                    loanId = $"NKS-{key}";
                }

                await using var command = new SqlCommand(InsertSql, connection);
                command.Parameters.AddWithValue("@non_ks_serviced_loan_key", key);
                AddCreateParameters(command, loan, loanId);

                await command.ExecuteNonQueryAsync(cancellationToken);

                var row = await ReadByKeyAsync(connection, key, cancellationToken);
                if (row is null)
                {
                    throw new InvalidOperationException(
                        $"Non-KS serviced loan {key} was created but could not be read back.");
                }

                created.Add(row);
            }

            _logger.LogInformation("Created {Count} non-KS serviced loan row(s).", created.Count);
            return created;
        }

        public async Task<bool> UpdateAsync(
            NonKsServicedLoanBulkUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Loans.Count == 0)
            {
                throw new InvalidOperationException("Request body must include at least one loan row.");
            }

            await EnsureTableAvailableAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                var validationError = NonKsServicedLoansValidation.ValidateUpdateItem(loan);
                if (validationError is not null)
                {
                    throw new InvalidOperationException(validationError);
                }

                var loanId = NormalizeOptional(loan.LoanId);
                if (!string.IsNullOrEmpty(loanId)
                    && await LoanIdExistsForOtherKeyAsync(
                        connection,
                        loanId,
                        loan.NonKsServicedLoanKey,
                        cancellationToken))
                {
                    throw new InvalidOperationException($"Loan ID '{loanId}' already exists.");
                }

                await using var command = new SqlCommand(UpdateSql, connection);
                command.Parameters.AddWithValue("@non_ks_serviced_loan_key", loan.NonKsServicedLoanKey);
                AddCreateParameters(command, loan, loanId);

                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Updated {AffectedRows} non-KS serviced loan row(s).", affectedRows);
                return true;
            }

            _logger.LogWarning("No non-KS serviced loan rows updated.");
            return false;
        }

        private async Task EnsureTableAvailableAsync(CancellationToken cancellationToken)
        {
            if (_tableAvailable == true)
            {
                return;
            }

            const string probeSql = "select top 0 non_ks_serviced_loan_key from mort.non_ks_serviced_loan";

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
                    "mort.non_ks_serviced_loan does not exist. Run Scripts/Create_mort_non_ks_serviced_loan.sql.");
            }
        }

        private static async Task<long> GetNextKeyAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(NextKeySql, connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }

        private static async Task<bool> LoanIdExistsAsync(
            SqlConnection connection,
            string loanId,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(LoanIdExistsSql, connection);
            command.Parameters.AddWithValue("@loan_id", loanId);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        private static async Task<bool> LoanIdExistsForOtherKeyAsync(
            SqlConnection connection,
            string loanId,
            long nonKsServicedLoanKey,
            CancellationToken cancellationToken)
        {
            const string sql = """
                select 1
                from mort.non_ks_serviced_loan
                where loan_id = @loan_id
                  and non_ks_serviced_loan_key <> @non_ks_serviced_loan_key
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@loan_id", loanId);
            command.Parameters.AddWithValue("@non_ks_serviced_loan_key", nonKsServicedLoanKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        private static async Task<NonKsServicedLoanRowDto?> ReadByKeyAsync(
            SqlConnection connection,
            long nonKsServicedLoanKey,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(SelectByKeySql, connection);
            command.Parameters.AddWithValue("@non_ks_serviced_loan_key", nonKsServicedLoanKey);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
        }

        private static void AddCreateParameters(
            SqlCommand command,
            NonKsServicedLoanCreateItem loan,
            string? loanId)
        {
            command.Parameters.AddWithValue("@loan_name", ToDbValue(NormalizeOptional(loan.LoanName)));
            command.Parameters.AddWithValue("@as_at_date", ToDbDate(loan.AsAtDate));
            command.Parameters.AddWithValue("@loan_id", ToDbValue(loanId));
            command.Parameters.AddWithValue("@servicer_id", ToDbValue(NormalizeOptional(loan.ServicerId)));
            command.Parameters.AddWithValue("@description", ToDbValue(NormalizeOptional(loan.Description)));
            command.Parameters.AddWithValue("@investor", ToDbValue(NormalizeOptional(loan.Investor)));
            command.Parameters.AddWithValue("@date_of_default", ToDbDate(loan.DateOfDefault));
            command.Parameters.AddWithValue("@maturity_date", ToDbDate(loan.MaturityDate));
            command.Parameters.AddWithValue("@interest_off_date", ToDbDate(loan.InterestOffDate));
            command.Parameters.AddWithValue("@tax_memo_date", ToDbDate(loan.TaxMemoDate));
            command.Parameters.AddWithValue("@security_value", ToDbDecimal(loan.SecurityValue));
            command.Parameters.AddWithValue("@units", ToDbInt(loan.Units));
            command.Parameters.AddWithValue("@net_acres", ToDbDecimal(loan.NetAcres));
            command.Parameters.AddWithValue("@square_feet", ToDbDecimal(loan.SquareFeet));
            command.Parameters.AddWithValue("@interest_rate", ToDbDecimal(loan.InterestRate));
            command.Parameters.AddWithValue("@principal_balance", ToDbDecimal(loan.PrincipalBalance));
            command.Parameters.AddWithValue("@outstanding_interest", ToDbDecimal(loan.OutstandingInterest));
            command.Parameters.AddWithValue("@accrued_interest", ToDbDecimal(loan.AccruedInterest));
            command.Parameters.AddWithValue("@late_interest", ToDbDecimal(loan.LateInterest));
            command.Parameters.AddWithValue("@outstanding_invoices", ToDbDecimal(loan.OutstandingInvoices));
            command.Parameters.AddWithValue("@est_realization_costs", ToDbDecimal(loan.EstRealizationCosts));
            command.Parameters.AddWithValue("@cost_to_complete", ToDbDecimal(loan.CostToComplete));
            command.Parameters.AddWithValue("@tax_arrears", ToDbDecimal(loan.TaxArrears));
            command.Parameters.AddWithValue("@interest_as_of_tax_memo", ToDbDecimal(loan.InterestAsOfTaxMemo));
            command.Parameters.AddWithValue("@interest_adjustment", ToDbDecimal(loan.InterestAdjustment));
            command.Parameters.AddWithValue("@user_updated_by", loan.UserUpdatedBy);
        }

        private static NonKsServicedLoanRowDto MapRow(SqlDataReader reader) =>
            new()
            {
                NonKsServicedLoanKey = GetInt64(reader, "non_ks_serviced_loan_key"),
                LoanName = GetNullableString(reader, "loan_name"),
                AsAtDate = GetNullableDate(reader, "as_at_date"),
                LoanId = GetNullableString(reader, "loan_id"),
                ServicerId = GetNullableString(reader, "servicer_id"),
                Description = GetNullableString(reader, "description"),
                Investor = GetNullableString(reader, "investor"),
                DateOfDefault = GetNullableDate(reader, "date_of_default"),
                MaturityDate = GetNullableDate(reader, "maturity_date"),
                InterestOffDate = GetNullableDate(reader, "interest_off_date"),
                TaxMemoDate = GetNullableDate(reader, "tax_memo_date"),
                SecurityValue = GetNullableDecimal(reader, "security_value"),
                Units = GetNullableInt32(reader, "units"),
                NetAcres = GetNullableDecimal(reader, "net_acres"),
                SquareFeet = GetNullableDecimal(reader, "square_feet"),
                InterestRate = GetNullableDecimal(reader, "interest_rate"),
                PrincipalBalance = GetNullableDecimal(reader, "principal_balance"),
                OutstandingInterest = GetNullableDecimal(reader, "outstanding_interest"),
                AccruedInterest = GetNullableDecimal(reader, "accrued_interest"),
                LateInterest = GetNullableDecimal(reader, "late_interest"),
                OutstandingInvoices = GetNullableDecimal(reader, "outstanding_invoices"),
                EstRealizationCosts = GetNullableDecimal(reader, "est_realization_costs"),
                CostToComplete = GetNullableDecimal(reader, "cost_to_complete"),
                TaxArrears = GetNullableDecimal(reader, "tax_arrears"),
                InterestAsOfTaxMemo = GetNullableDecimal(reader, "interest_as_of_tax_memo"),
                InterestAdjustment = GetNullableDecimal(reader, "interest_adjustment"),
                UserUpdatedBy = GetNullableString(reader, "user_updated_by"),
                UserUpdatedDate = GetNullableDateTime(reader, "user_updated_date")
            };

        private static object ToDbValue(string? value) =>
            string.IsNullOrEmpty(value) ? DBNull.Value : value;

        private static object ToDbDate(DateTime? value) =>
            value.HasValue ? value.Value.Date : DBNull.Value;

        private static object ToDbDecimal(decimal? value) =>
            value.HasValue ? value.Value : DBNull.Value;

        private static object ToDbInt(int? value) =>
            value.HasValue ? value.Value : DBNull.Value;

        private static string? NormalizeOptional(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static long GetInt64(SqlDataReader reader, string name) =>
            Convert.ToInt64(reader.GetValue(reader.GetOrdinal(name)));

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

        private static int? GetNullableInt32(SqlDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
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
