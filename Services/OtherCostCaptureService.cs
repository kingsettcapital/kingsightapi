using System.Text;

using kingsightapi.Entities;

using Microsoft.Data.SqlClient;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.Logging;



namespace kingsightapi.Services

{

    public interface IOtherCostCaptureService

    {

        Task<IReadOnlyList<OtherCostCaptureDto>> GetAsync(

            int? loanAliasId,

            IReadOnlyList<string>? statuses,

            CancellationToken cancellationToken = default);



        Task<bool> UpdateAsync(

            OtherCostCaptureBatchUpdateRequest request,

            string auditDisplayName,

            CancellationToken cancellationToken = default);

    }



    public sealed class OtherCostCaptureService : IOtherCostCaptureService

    {

        private readonly string _connectionString;

        private readonly SubjectiveInputSql _sql;

        private readonly ILogger<OtherCostCaptureService> _logger;



        private bool _schemaProbed;

        private SubjectiveInputRelationshipAuditColumns _auditColumns = new();

        private string? _loanStatusKeyColumn;



        public OtherCostCaptureService(

            IConfiguration configuration,

            ILogger<OtherCostCaptureService> logger,

            FabricWarehouseTables tables)

        {

            _connectionString = configuration.GetConnectionString("FabricConnectionString")

                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");

            _logger = logger;

            _sql = new SubjectiveInputSql(tables);

        }



        public async Task<IReadOnlyList<OtherCostCaptureDto>> GetAsync(

            int? loanAliasId,

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



            var sql = BuildListSql(loanAliasId, statusFilter, loanStatusKeyColumn);



            await using var connection = new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);



            await using var command = new SqlCommand(sql, connection);

            if (loanAliasId is > 0)

            {

                command.Parameters.AddWithValue("@loan_alias_id", loanAliasId.Value);

            }



            LoanStatusFilterParser.AddParameters(command, statusFilter);



            try

            {

                return await ReadRowsAsync(command, loanAliasId, cancellationToken);

            }

            catch (SqlException ex) when (statusFilter.HasFilter)

            {

                _logger.LogError(

                    ex,

                    "Other cost capture query failed with status filter (column={Column}).",

                    loanStatusKeyColumn);

                throw;

            }

        }



        public async Task<bool> UpdateAsync(

            OtherCostCaptureBatchUpdateRequest request,

            string auditDisplayName,

            CancellationToken cancellationToken = default)

        {

            await EnsureSchemaAsync(cancellationToken);



            await using var connection = new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);



            var affectedRows = 0;

            foreach (var loan in request.Loans)

            {

                var rowsChanged = loan.LoanKey > 0

                    ? await ExecuteUpdateAsync(

                        BuildUpdateByLoanKeySql(),

                        loan,

                        auditDisplayName,

                        connection,

                        cancellationToken)

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

            }



            if (affectedRows > 0)

            {

                _logger.LogInformation("Updated {AffectedRows} other cost capture loan rows.", affectedRows);

                return true;

            }



            _logger.LogWarning("No other cost capture loan rows updated.");

            return false;

        }



        private async Task<IReadOnlyList<OtherCostCaptureDto>> ReadRowsAsync(

            SqlCommand command,

            int? loanAliasId,

            CancellationToken cancellationToken)

        {

            var rows = new List<OtherCostCaptureDto>();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var ordinals = GetOrdinals(reader);



            while (await reader.ReadAsync(cancellationToken))

            {

                rows.Add(MapRow(reader, ordinals));

            }



            _logger.LogInformation(

                "Retrieved {Count} other cost capture rows (loanAliasId={LoanAliasId}).",

                rows.Count,

                loanAliasId);



            return rows;

        }



        private async Task<int> ExecuteUpdateAsync(

            string sql,

            OtherCostCaptureUpdateDto loan,

            string auditDisplayName,

            SqlConnection connection,

            CancellationToken cancellationToken)

        {

            await using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@loan_key", loan.LoanKey);

            command.Parameters.AddWithValue("@loan_code", loan.LoanCode?.Trim() ?? string.Empty);

            command.Parameters.AddWithValue(

                "@outstanding_invoice_value",

                loan.OutstandingInvoices.HasValue ? loan.OutstandingInvoices.Value : DBNull.Value);

            command.Parameters.AddWithValue(

                "@estimated_realization_value",

                loan.EstRealizationCosts.HasValue ? loan.EstRealizationCosts.Value : DBNull.Value);

            command.Parameters.AddWithValue(

                "@cost_to_complete_value",

                loan.CostToComplete.HasValue ? loan.CostToComplete.Value : DBNull.Value);

            _auditColumns.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);



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

            _schemaProbed = true;

        }



        private string BuildListSql(int? loanAliasId, LoanStatusFilter statusFilter, string? loanStatusKeyColumn)

        {

            var sql = new StringBuilder(

                $"""

                 select {SubjectiveInputSql.LoanKeySelect("r", "l")},

                        r.loan_code,

                        r.loan_description,

                        r.loan_alias_name,

                        r.outstanding_invoice,

                        r.estimated_realization_costs,

                        r.cost_to_complete,

                        user_updated_by = {_auditColumns.BuildSelectUpdatedByExpression()},

                        user_updated_date = {_auditColumns.BuildSelectUpdatedDtmExpression()}

                 from {_sql.LoanAliasRelationship} r

                 {_sql.SharedDimLoanOuterApplyOnLoanCode("r", "l")}

                 """);



            if (loanAliasId is > 0)

            {

                sql.AppendLine(

                    $"""

                     inner join {_sql.LoanAliasMaster} m

                         on r.loan_alias_name = m.loan_alias_name

                        and m.loan_alias_id = @loan_alias_id

                     """);

            }



            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))

            {

                sql.AppendLine(" where 1 = 1");

                LoanStatusFilterParser.AppendExistsSqlCondition(

                    sql,

                    "r",

                    _sql.SharedDimLoan,

                    loanStatusKeyColumn,

                    statusFilter,

                    _sql.DimStatus);

            }



            sql.AppendLine();

            sql.Append(" order by r.loan_alias_name, r.loan_code");

            return sql.ToString();

        }



        private string BuildUpdateByLoanKeySql() =>

            $"""

                update r

                set outstanding_invoice = @outstanding_invoice_value,

                    estimated_realization_costs = @estimated_realization_value,

                    cost_to_complete = @cost_to_complete_value{_auditColumns.BuildUpdateSetClause()}

                from {_sql.LoanAliasRelationship} r

                inner join {_sql.SharedDimLoan} l

                    on l.loan_key = @loan_key

                   and {SubjectiveInputSql.EqualsVarchar("l", "loan_code", "r", "loan_code")}

                   and {SubjectiveInputSql.DimLoanIsCurrent("l")}

                """;



        private string BuildUpdateByLoanCodeSql() =>

            $"""

                update r

                set outstanding_invoice = @outstanding_invoice_value,

                    estimated_realization_costs = @estimated_realization_value,

                    cost_to_complete = @cost_to_complete_value{_auditColumns.BuildUpdateSetClause()}

                from {_sql.LoanAliasRelationship} r

                where cast(r.loan_code as varchar(100)) collate database_default = cast(@loan_code as varchar(100)) collate database_default

                """;



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

                    _sql.SharedDimLoan,

                    cancellationToken);



                _logger.LogInformation(

                    "Using shared.dim_loan.{Column} for other cost capture status filter.",

                    _loanStatusKeyColumn);



                return _loanStatusKeyColumn;

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Other cost capture status filter skipped; shared.dim_loan status column unavailable.");

                return null;

            }

        }



        private static OtherCostCaptureDto MapRow(

            SqlDataReader reader,

            (

                int Key,

                int Code,

                int Desc,

                int AliasName,

                int Outstanding,

                int EstRealization,

                int CostToComplete,

                int UpdatedBy,

                int UpdatedDate) ordinals)

        {

            DateTime? updatedDate = null;

            if (!reader.IsDBNull(ordinals.UpdatedDate))

            {

                updatedDate = DateTime.SpecifyKind(reader.GetDateTime(ordinals.UpdatedDate), DateTimeKind.Utc);

            }



            return new OtherCostCaptureDto

            {

                LoanKey = reader.IsDBNull(ordinals.Key)

                    ? 0L

                    : Convert.ToInt64(reader.GetValue(ordinals.Key)),

                LoanId = reader.IsDBNull(ordinals.Code)

                    ? string.Empty

                    : reader.GetString(ordinals.Code),

                Description = reader.IsDBNull(ordinals.Desc)

                    ? string.Empty

                    : reader.GetString(ordinals.Desc),

                LoanAliasName = reader.IsDBNull(ordinals.AliasName)

                    ? string.Empty

                    : reader.GetString(ordinals.AliasName),

                OutstandingInvoices = GetNullableDecimal(reader, ordinals.Outstanding),

                EstRealizationCosts = GetNullableDecimal(reader, ordinals.EstRealization),

                CostToComplete = GetNullableDecimal(reader, ordinals.CostToComplete),

                UserUpdatedBy = reader.IsDBNull(ordinals.UpdatedBy)

                    ? string.Empty

                    : reader.GetString(ordinals.UpdatedBy),

                UserUpdatedDate = updatedDate

            };

        }



        private static (

            int Key,

            int Code,

            int Desc,

            int AliasName,

            int Outstanding,

            int EstRealization,

            int CostToComplete,

            int UpdatedBy,

            int UpdatedDate) GetOrdinals(SqlDataReader reader)

        {

            return (

                reader.GetOrdinal("loan_key"),

                reader.GetOrdinal("loan_code"),

                reader.GetOrdinal("loan_description"),

                reader.GetOrdinal("loan_alias_name"),

                reader.GetOrdinal("outstanding_invoice"),

                reader.GetOrdinal("estimated_realization_costs"),

                reader.GetOrdinal("cost_to_complete"),

                reader.GetOrdinal("user_updated_by"),

                reader.GetOrdinal("user_updated_date"));

        }



        private static decimal? GetNullableDecimal(SqlDataReader reader, int ordinal) =>

            reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));

    }

}


