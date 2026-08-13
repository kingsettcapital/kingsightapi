using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace kingsightapi.Services
{
    public interface INonKsServicedLoansService
    {
        Task<NonKsServicedLoanLookupsDto> GetLookupsAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<NonKsServicedLoanRowDto>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<NonKsServicedLoanRowDto>> CreateAsync(
            NonKsServicedLoanBulkCreateRequest request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<NonKsServicedLoanRowDto>> UpdateAsync(
            NonKsServicedLoanBulkUpdateRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed class NonKsServicedLoansService : INonKsServicedLoansService
    {
        private const string ExtLoanCodePrefix = "NKSLn-";

        private readonly string _connectionString;
        private readonly string _tblExternalServicedLoan;
        private readonly string _vwLoanAttributes;
        private readonly INonKsInvestorAliasBridge _investorAliasBridge;
        private readonly ILogger<NonKsServicedLoansService> _logger;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private ExternalServicedLoanColumnMap? _columns;

        public NonKsServicedLoansService(
            IConfiguration configuration,
            ILogger<NonKsServicedLoansService> logger,
            FabricWarehouseTables tables,
            INonKsInvestorAliasBridge investorAliasBridge)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _investorAliasBridge = investorAliasBridge;
            _tblExternalServicedLoan = tables.SubjectiveInput("external_serviced_loan");
            _vwLoanAttributes = tables.Mortgage("vw_loan_attributes");
        }

        public async Task<NonKsServicedLoanLookupsDto> GetLookupsAsync(
            CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            var columns = await GetColumnsAsync(cancellationToken);

            return new NonKsServicedLoanLookupsDto
            {
                NextExtLoanCode = await GetNextExtLoanCodeAsync(connection, cancellationToken),
                Sponsors = await LoadSponsorOptionsAsync(connection, columns, cancellationToken)
            };
        }

        public async Task<IReadOnlyList<NonKsServicedLoanRowDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var columns = await GetColumnsAsync(cancellationToken);

            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand(columns.BuildListSql(), connection);

            var rows = new List<NonKsServicedLoanRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation("Retrieved {Count} external serviced loan rows.", rows.Count);
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

            var columns = await GetColumnsAsync(cancellationToken);

            await using var connection = await OpenConnectionAsync(cancellationToken);

            var created = new List<NonKsServicedLoanRowDto>();
            var nextExtLoanCode = await GetNextExtLoanCodeAsync(connection, cancellationToken);

            foreach (var loan in request.Loans)
            {
                var validationError = NonKsServicedLoansValidation.ValidateCreateItem(loan);
                if (validationError is not null)
                {
                    throw new InvalidOperationException(validationError);
                }

                string extLoanCode;
                if (TryResolveProvidedExtLoanCode(loan, out var providedExtLoanCode))
                {
                    extLoanCode = providedExtLoanCode;
                }
                else
                {
                    extLoanCode = nextExtLoanCode;
                    nextExtLoanCode = IncrementExtLoanCode(extLoanCode);
                }

                if (await RowExistsAsync(connection, columns, extLoanCode, loan.AsAtDate, cancellationToken))
                {
                    var asAtLabel = loan.AsAtDate?.ToString("yyyy-MM-dd") ?? "(none)";
                    throw new InvalidOperationException(
                        $"A record already exists for Loan ID '{extLoanCode}' and As At date '{asAtLabel}'.");
                }

                var auditUtc = DateTime.UtcNow;
                await using var command = new SqlCommand(columns.BuildInsertSql(), connection);
                command.Parameters.AddWithValue("@ext_loan_code", extLoanCode);
                AddWriteParameters(command, columns, loan);
                columns.AddInsertAuditParameters(command, loan.UserUpdatedBy, auditUtc);

                await command.ExecuteNonQueryAsync(cancellationToken);

                try
                {
                    var investorCode = NormalizeOptional(loan.InvestorCode);
                    if (!string.IsNullOrWhiteSpace(investorCode))
                    {
                        await _investorAliasBridge.EnsureRelationshipRowAsync(
                            connection,
                            investorCode,
                            ResolveInvestorAliasName(loan) ?? loan.Investor,
                            null,
                            loan.UserUpdatedBy,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Non-KS loan {ExtLoanCode} created but failed to register investor on Investor Alias Assignment.",
                        extLoanCode);
                }

                var row = await ReadByKeyAsync(connection, columns, extLoanCode, loan.AsAtDate, cancellationToken);
                if (row is null)
                {
                    throw new InvalidOperationException(
                        $"External serviced loan '{extLoanCode}' was created but could not be read back.");
                }

                created.Add(row);
            }

            _logger.LogInformation("Created {Count} external serviced loan row(s).", created.Count);
            return created;
        }

        public async Task<IReadOnlyList<NonKsServicedLoanRowDto>> UpdateAsync(
            NonKsServicedLoanBulkUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Loans.Count == 0)
            {
                throw new InvalidOperationException("Request body must include at least one loan row.");
            }

            var columns = await GetColumnsAsync(cancellationToken);

            await using var connection = await OpenConnectionAsync(cancellationToken);

            var updated = new List<NonKsServicedLoanRowDto>();
            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                var validationError = NonKsServicedLoansValidation.ValidateUpdateItem(loan);
                if (validationError is not null)
                {
                    throw new InvalidOperationException(validationError);
                }

                var extLoanCode = ResolveExtLoanCode(loan);
                var originalAsAtDate = loan.OriginalAsAtDate ?? loan.AsAtDate;

                await using var command = new SqlCommand(columns.BuildUpdateSql(), connection);
                command.Parameters.AddWithValue("@ext_loan_code", extLoanCode);
                command.Parameters.AddWithValue("@original_as_at_date", ToDbDate(originalAsAtDate));
                AddWriteParameters(command, columns, loan);
                columns.AddUpdateAuditParameters(command, loan.UserUpdatedBy, DateTime.UtcNow);

                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);

                try
                {
                    var investorCode = NormalizeOptional(loan.InvestorCode);
                    if (!string.IsNullOrWhiteSpace(investorCode))
                    {
                        await _investorAliasBridge.EnsureRelationshipRowAsync(
                            connection,
                            investorCode,
                            ResolveInvestorAliasName(loan) ?? loan.Investor,
                            null,
                            loan.UserUpdatedBy,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Non-KS loan updated but failed to register investor on Investor Alias Assignment.");
                }

                var row = await ReadByKeyAsync(connection, columns, extLoanCode, loan.AsAtDate, cancellationToken);
                if (row is null && originalAsAtDate != loan.AsAtDate)
                {
                    row = await ReadByKeyAsync(connection, columns, extLoanCode, originalAsAtDate, cancellationToken);
                }

                if (row is not null)
                {
                    updated.Add(row);
                }
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Updated {AffectedRows} external serviced loan row(s).", affectedRows);
                return updated;
            }

            _logger.LogWarning("No external serviced loan rows updated.");
            throw new InvalidOperationException(
                "No external serviced loan rows were updated. Verify Loan ID and As At date match an existing row.");
        }

        private async Task<ExternalServicedLoanColumnMap> GetColumnsAsync(CancellationToken cancellationToken)
        {
            if (_columns is not null)
            {
                return _columns;
            }

            await _schemaLock.WaitAsync(cancellationToken);
            try
            {
                if (_columns is not null)
                {
                    return _columns;
                }

                _columns = await ExternalServicedLoanColumnMap.ProbeAsync(
                    _connectionString,
                    _tblExternalServicedLoan,
                    cancellationToken);
                return _columns;
            }
            catch (SqlException ex) when (ex.Number is 208 or 3701)
            {
                throw new InvalidOperationException(
                    "subjective_input.external_serviced_loan does not exist. Verify wh_gold1 subjective_input schema.");
            }
            finally
            {
                _schemaLock.Release();
            }
        }

        private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        /// <summary>
        /// Unique sponsor names for Non-KS dropdown: Yardi vw_loan_attributes + Non-KS rows.
        /// Add-new on the SPA is free-text until saved on the loan (no separate sponsor master).
        /// </summary>
        private async Task<IReadOnlyList<string>> LoadSponsorOptionsAsync(
            SqlConnection connection,
            ExternalServicedLoanColumnMap columns,
            CancellationToken cancellationToken)
        {
            var sponsors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                await using var yardiCommand = new SqlCommand(
                    $"""
                    select distinct sponsor = ltrim(rtrim(sponsor))
                    from {_vwLoanAttributes}
                    where sponsor is not null
                      and ltrim(rtrim(sponsor)) <> ''
                    """,
                    connection);
                await using var yardiReader = await yardiCommand.ExecuteReaderAsync(cancellationToken);
                while (await yardiReader.ReadAsync(cancellationToken))
                {
                    var sponsor = yardiReader.IsDBNull(0) ? null : Convert.ToString(yardiReader.GetValue(0));
                    if (!string.IsNullOrWhiteSpace(sponsor))
                    {
                        sponsors.Add(sponsor.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Non-KS sponsor lookup skipped vw_loan_attributes.");
            }

            if (columns.Sponsor is not null)
            {
                try
                {
                    await using var nonKsCommand = new SqlCommand(
                        $"""
                        select distinct sponsor = ltrim(rtrim([{columns.Sponsor}]))
                        from {_tblExternalServicedLoan}
                        where [{columns.Sponsor}] is not null
                          and ltrim(rtrim([{columns.Sponsor}])) <> ''
                        """,
                        connection);
                    await using var nonKsReader = await nonKsCommand.ExecuteReaderAsync(cancellationToken);
                    while (await nonKsReader.ReadAsync(cancellationToken))
                    {
                        var sponsor = nonKsReader.IsDBNull(0) ? null : Convert.ToString(nonKsReader.GetValue(0));
                        if (!string.IsNullOrWhiteSpace(sponsor))
                        {
                            sponsors.Add(sponsor.Trim());
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Non-KS sponsor lookup skipped external_serviced_loan.sponsor.");
                }
            }

            return sponsors.ToList();
        }

        private async Task<string> GetNextExtLoanCodeAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            var columns = await GetColumnsAsync(cancellationToken);
            var sql = $"""
                select [{columns.ExtLoanCode}]
                from {_tblExternalServicedLoan}
                where [{columns.ExtLoanCode}] like @prefixNew
                   or [{columns.ExtLoanCode}] like @prefixLegacy
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@prefixNew", $"{ExtLoanCodePrefix}%");
            command.Parameters.AddWithValue("@prefixLegacy", "NONKS-%");

            var maxNumber = 0;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0));
                var number = ParseExtLoanCodeNumber(code);
                if (number > maxNumber)
                {
                    maxNumber = number;
                }
            }

            return $"{ExtLoanCodePrefix}{maxNumber + 1}";
        }

        private static string IncrementExtLoanCode(string extLoanCode)
        {
            var number = ParseExtLoanCodeNumber(extLoanCode);
            return $"{ExtLoanCodePrefix}{number + 1}";
        }

        private static int ParseExtLoanCodeNumber(string? extLoanCode)
        {
            if (string.IsNullOrWhiteSpace(extLoanCode))
            {
                return 0;
            }

            var match = Regex.Match(extLoanCode.Trim(), @"^(?:NKSLn|NONKS)-(\d+)$", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out var parsed) ? parsed : 0;
        }

        private static bool TryResolveProvidedExtLoanCode(
            NonKsServicedLoanCreateItem loan,
            out string extLoanCode)
        {
            var candidate = NormalizeOptional(loan.ExtLoanCode)
                ?? NormalizeOptional(loan.LoanCode)
                ?? NormalizeOptional(loan.LoanId);
            if (candidate is null)
            {
                extLoanCode = string.Empty;
                return false;
            }

            extLoanCode = candidate;
            return true;
        }

        private static string ResolveExtLoanCode(NonKsServicedLoanUpdateItem loan)
        {
            var extLoanCode = NormalizeOptional(loan.ExtLoanCode)
                ?? NormalizeOptional(loan.LoanCode)
                ?? NormalizeOptional(loan.LoanId);
            if (extLoanCode is null)
            {
                throw new InvalidOperationException("Loan ID is required for update.");
            }

            return extLoanCode;
        }

        private async Task<bool> RowExistsAsync(
            SqlConnection connection,
            ExternalServicedLoanColumnMap columns,
            string extLoanCode,
            DateTime? asAtDate,
            CancellationToken cancellationToken)
        {
            return await ReadByKeyAsync(connection, columns, extLoanCode, asAtDate, cancellationToken)
                is not null;
        }

        private async Task<NonKsServicedLoanRowDto?> ReadByKeyAsync(
            SqlConnection connection,
            ExternalServicedLoanColumnMap columns,
            string extLoanCode,
            DateTime? asAtDate,
            CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(columns.BuildSelectByKeySql(), connection);
            command.Parameters.AddWithValue("@ext_loan_code", extLoanCode);
            command.Parameters.AddWithValue("@as_at_date", ToDbDate(asAtDate));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
        }

        private static void AddWriteParameters(
            SqlCommand command,
            ExternalServicedLoanColumnMap columns,
            NonKsServicedLoanCreateItem loan)
        {
            var loanAliasName = ResolveLoanAliasName(loan);
            var investorAliasName = ResolveInvestorAliasName(loan);

            if (columns.LoanAliasName is not null)
            {
                command.Parameters.AddWithValue("@loan_alias_name", ToDbValue(loanAliasName));
            }

            if (columns.AsAtDate is not null)
            {
                command.Parameters.AddWithValue("@as_at_date", ToDbDate(loan.AsAtDate));
            }

            if (columns.ServicerId is not null)
            {
                command.Parameters.AddWithValue("@servicer_id", ToDbValue(NormalizeOptional(loan.ServicerId)));
            }

            if (columns.Description is not null)
            {
                command.Parameters.AddWithValue("@description", ToDbValue(NormalizeOptional(loan.Description)));
            }

            if (columns.InvestorAliasName is not null)
            {
                command.Parameters.AddWithValue("@investor_alias_name", ToDbValue(investorAliasName));
            }

            if (columns.InvestorCode is not null)
            {
                command.Parameters.AddWithValue(
                    "@investor_code",
                    ToDbValue(NormalizeOptional(loan.InvestorCode)));
            }

            if (columns.Sponsor is not null)
            {
                command.Parameters.AddWithValue(
                    "@sponsor",
                    ToDbValue(NormalizeOptional(loan.Sponsor)));
            }

            AddOptionalDate(command, columns.DefaultDate, "@default_date", loan.DateOfDefault);
            AddOptionalDate(command, columns.MaturityDate, "@maturity_date", loan.MaturityDate);
            AddOptionalDate(command, columns.InterestOffDate, "@interest_off_date", loan.InterestOffDate);
            AddOptionalDate(command, columns.TaxMemoDate, "@tax_memo_date", loan.TaxMemoDate);
            AddOptionalDecimal(command, columns.SecurityValue, "@security_value", loan.SecurityValue);
            AddOptionalInt(command, columns.Units, "@units", loan.Units);
            AddOptionalDecimal(command, columns.NetAcres, "@net_acres", loan.NetAcres);
            AddOptionalDecimal(command, columns.SquareFeet, "@square_feet", loan.SquareFeet);
            AddOptionalDecimal(command, columns.InterestRate, "@interest_rate", loan.InterestRate);
            AddOptionalDecimal(command, columns.CurrentLtv, "@current_ltv", loan.CurrentLtv);
            AddOptionalDecimal(command, columns.PrincipalBalance, "@principal_balance", loan.PrincipalBalance);
            AddOptionalDecimal(command, columns.OutstandingInterest, "@outstanding_interest", loan.OutstandingInterest);
            AddOptionalDecimal(command, columns.AccruedInterest, "@accrued_interest", loan.AccruedInterest);
            AddOptionalDecimal(command, columns.LateInterest, "@late_interest", loan.LateInterest);
            AddOptionalDecimal(command, columns.OutstandingInvoices, "@outstanding_invoices", loan.OutstandingInvoices);
            AddOptionalDecimal(command, columns.EstRealizationCosts, "@est_realization_costs", loan.EstRealizationCosts);
            AddOptionalDecimal(command, columns.CostToComplete, "@cost_to_complete", loan.CostToComplete);
            AddOptionalDecimal(command, columns.TaxArrears, "@tax_arrears", loan.TaxArrears);
            AddOptionalDecimal(command, columns.InterestAsOfTaxMemo, "@interest_as_of_tax_memo", loan.InterestAsOfTaxMemo);
            AddOptionalDecimal(command, columns.InterestAdjustment, "@interest_adjustment", loan.InterestAdjustment);

            if (columns.FundingStatus is not null)
            {
                command.Parameters.AddWithValue(
                    "@funding_status",
                    ToDbValue(NormalizeOptional(loan.FundingStatus)));
            }
        }

        private static NonKsServicedLoanRowDto MapRow(SqlDataReader reader)
        {
            var extLoanCode = GetNullableString(reader, "ext_loan_code");
            var asAtDate = GetNullableDate(reader, "as_at_date");
            var loanAliasName = GetNullableString(reader, "loan_alias_name");
            var investorName = GetNullableString(reader, "investor_alias_name");
            var investorCode = GetNullableString(reader, "investor_code");

            return new NonKsServicedLoanRowDto
            {
                NonKsServicedLoanKey = ComputeRowKey(extLoanCode, asAtDate),
                LoanAliasName = loanAliasName,
                LoanName = loanAliasName,
                AsAtDate = asAtDate,
                LoanId = extLoanCode,
                LoanCode = extLoanCode,
                ExtLoanCode = extLoanCode,
                ServicerId = GetNullableString(reader, "servicer_id"),
                Description = GetNullableString(reader, "description"),
                InvestorAliasName = investorName,
                Investor = investorName,
                InvestorCode = investorCode,
                Sponsor = GetNullableString(reader, "sponsor"),
                DateOfDefault = GetNullableDate(reader, "default_date"),
                MaturityDate = GetNullableDate(reader, "maturity_date"),
                InterestOffDate = GetNullableDate(reader, "interest_off_date"),
                TaxMemoDate = GetNullableDate(reader, "tax_memo_date"),
                SecurityValue = GetNullableDecimal(reader, "security_value"),
                Units = GetNullableInt32(reader, "units"),
                NetAcres = GetNullableDecimal(reader, "net_acres"),
                SquareFeet = GetNullableDecimal(reader, "square_feet"),
                InterestRate = GetNullableDecimal(reader, "interest_rate"),
                CurrentLtv = GetNullableDecimal(reader, "current_ltv"),
                PrincipalBalance = GetNullableDecimal(reader, "principal_balance"),
                OutstandingInterest = GetNullableDecimal(reader, "outstanding_interest"),
                AccruedInterest = GetNullableDecimal(reader, "accrued_interest"),
                LateInterest = GetNullableDecimal(reader, "late_interest"),
                OutstandingInvoices = GetNullableDecimal(reader, "outstanding_invoice"),
                EstRealizationCosts = GetNullableDecimal(reader, "estimated_realization_costs"),
                CostToComplete = GetNullableDecimal(reader, "cost_to_complete"),
                TaxArrears = GetNullableDecimal(reader, "tax_arrears"),
                InterestAsOfTaxMemo = GetNullableDecimal(reader, "interest_as_of_tax_memo"),
                InterestAdjustment = GetNullableDecimal(reader, "interest_adjustment"),
                FundingStatus = GetNullableString(reader, "funding_status"),
                UserUpdatedBy = GetNullableString(reader, "updated_by"),
                UserUpdatedDate = GetNullableDateTime(reader, "updated_datetime"),
                CreatedBy = GetNullableString(reader, "created_by"),
                CreatedDate = GetNullableDateTime(reader, "created_datetime")
            };
        }

        private static long ComputeRowKey(string? extLoanCode, DateTime? asAtDate)
        {
            unchecked
            {
                long hash = 5381;
                foreach (var c in (extLoanCode ?? string.Empty).ToUpperInvariant())
                {
                    hash = ((hash << 5) + hash) ^ c;
                }

                if (asAtDate.HasValue)
                {
                    hash = ((hash << 5) + hash) ^ asAtDate.Value.Ticks;
                }

                return hash == long.MinValue ? 1 : Math.Abs(hash);
            }
        }

        private static void AddOptionalDate(SqlCommand command, string? column, string parameter, DateTime? value)
        {
            if (column is not null)
            {
                command.Parameters.AddWithValue(parameter, ToDbDate(value));
            }
        }

        private static void AddOptionalDecimal(SqlCommand command, string? column, string parameter, decimal? value)
        {
            if (column is not null)
            {
                command.Parameters.AddWithValue(parameter, ToDbDecimal(value));
            }
        }

        private static void AddOptionalInt(SqlCommand command, string? column, string parameter, int? value)
        {
            if (column is not null)
            {
                command.Parameters.AddWithValue(parameter, ToDbInt(value));
            }
        }

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

        private static string? ResolveLoanAliasName(NonKsServicedLoanCreateItem loan) =>
            NormalizeOptional(loan.LoanAliasName) ?? NormalizeOptional(loan.LoanName);

        private static string? ResolveInvestorAliasName(NonKsServicedLoanCreateItem loan) =>
            NormalizeOptional(loan.InvestorAliasName) ?? NormalizeOptional(loan.Investor);

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
