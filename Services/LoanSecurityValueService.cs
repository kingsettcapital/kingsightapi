using System.Text;

using kingsightapi.Entities;

using Microsoft.Data.SqlClient;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.Logging;



namespace kingsightapi.Services

{

    public interface ILoanSecurityValueService

    {

        Task<IReadOnlyList<LoanSecurityValueDto>> GetAllAsync(

            IReadOnlyList<long>? loanAliasIds = null,

            IReadOnlyList<string>? statuses = null);



        Task<IReadOnlyList<LoanSecurityValueStatusOptionDto>> GetStatusOptionsAsync();



        Task<bool> UpdateAsync(LoanSecurityValueBatchUpdateRequest request, string auditDisplayName);

    }



    public sealed class LoanSecurityValueService : ILoanSecurityValueService

    {

        private readonly string _connectionString;

        private readonly string _tblDimLoan;

        private readonly string _tblLoanAliasMaster;

        private readonly string _tblLoanAliasRelationship;

        private readonly string _tblDimStatus;

        private readonly ILogger<LoanSecurityValueService> _logger;



        private bool _schemaProbed;

        private SubjectiveInputMasterAuditColumns _auditColumns = new();

        private string? _loanStatusKeyColumn;



        public LoanSecurityValueService(

            IConfiguration configuration,

            ILogger<LoanSecurityValueService> logger,

            FabricWarehouseTables tables)

        {

            _connectionString = configuration.GetConnectionString("FabricConnectionString")

                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");

            _logger = logger;



            var subjective = new SubjectiveInputSql(tables);

            _tblDimLoan = subjective.SharedDimLoan;

            _tblLoanAliasMaster = subjective.LoanAliasMaster;

            _tblLoanAliasRelationship = subjective.LoanAliasRelationship;

            _tblDimStatus = subjective.DimStatus;
        }



        public async Task<IReadOnlyList<LoanSecurityValueDto>> GetAllAsync(

            IReadOnlyList<long>? loanAliasIds = null,

            IReadOnlyList<string>? statuses = null)

        {

            await EnsureSchemaAsync();



            var statusFilter = LoanStatusFilterParser.Parse(statuses);

            string? loanStatusKeyColumn = null;

            if (statusFilter.HasFilter)

            {

                loanStatusKeyColumn = await TryResolveLoanStatusKeyColumnAsync();

            }



            var sql = BuildListSql(loanAliasIds, statusFilter, loanStatusKeyColumn);



            await using var connection = new SqlConnection(_connectionString);

            await connection.OpenAsync();



            await using var command = new SqlCommand(sql, connection)

            {

                CommandType = System.Data.CommandType.Text

            };

            AddFilterParameters(command, loanAliasIds, statusFilter);



            try

            {

                return await ReadRowsAsync(command);

            }

            catch (SqlException ex) when (statusFilter.HasFilter)

            {

                _logger.LogWarning(

                    ex,

                    "Loan security value query failed with status filter; retrying without status filter.");

                return await GetAllAsync(loanAliasIds, null);

            }

        }



        public async Task<IReadOnlyList<LoanSecurityValueStatusOptionDto>> GetStatusOptionsAsync()

        {

            var options = new List<LoanSecurityValueStatusOptionDto>

            {

                new()

                {

                    Value = LoanSecurityValueStatusTokens.NullValue,

                    DisplayLabel = "(Not set)"

                }

            };



            try

            {

                await using var connection = new SqlConnection(_connectionString);

                await connection.OpenAsync();



                var statusOptionsSql = $"""

                    select s.status_key,

                           s.status_name

                    from {_tblDimStatus} s

                    order by s.status_name

                    """;



                await using var command = new SqlCommand(statusOptionsSql, connection);

                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())

                {

                    var statusKey = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0));

                    var statusName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);

                    if (statusKey <= 0 || string.IsNullOrWhiteSpace(statusName))

                    {

                        continue;

                    }



                    options.Add(new LoanSecurityValueStatusOptionDto

                    {

                        Value = statusKey.ToString(),

                        DisplayLabel = statusName

                    });

                }

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Status filter options skipped; dim_status unavailable.");

            }



            return options;

        }



        public async Task<bool> UpdateAsync(LoanSecurityValueBatchUpdateRequest request, string auditDisplayName)

        {

            await EnsureSchemaAsync();



            var updateSql = BuildUpdateSql();



            await using var connection = new SqlConnection(_connectionString);

            await connection.OpenAsync();



            var affectedRows = 0;

            foreach (var item in request.LoanSecurityValues)

            {

                await using var command = new SqlCommand(updateSql, connection)

                {

                    CommandType = System.Data.CommandType.Text

                };



                command.Parameters.AddWithValue("@loan_alias_id", item.LoanAliasId);

                command.Parameters.AddWithValue(

                    "@security_value",

                    item.SecurityValue.HasValue ? item.SecurityValue.Value : DBNull.Value);

                command.Parameters.AddWithValue(

                    "@units",

                    item.Units.HasValue ? item.Units.Value : DBNull.Value);

                command.Parameters.AddWithValue(

                    "@square_feet",

                    item.SquareFeet.HasValue ? item.SquareFeet.Value : DBNull.Value);

                command.Parameters.AddWithValue(

                    "@acres",

                    item.Acres.HasValue ? item.Acres.Value : DBNull.Value);

                _auditColumns.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);



                affectedRows += await command.ExecuteNonQueryAsync();

            }



            if (affectedRows > 0)

            {

                _logger.LogInformation("Updated {AffectedRows} loan security value rows.", affectedRows);

                return true;

            }



            _logger.LogWarning("No loan security value rows updated.");

            return false;

        }



        private async Task EnsureSchemaAsync()
        {
            if (_schemaProbed)
            {
                return;
            }

            _auditColumns = await SubjectiveInputMasterAuditColumns.ProbeAsync(
                _connectionString,
                _tblLoanAliasMaster);
            _schemaProbed = true;
        }

        private string BuildUpdateSql() =>
            $"""
                update {_tblLoanAliasMaster}
                set security_value = @security_value,
                    units = @units,
                    square_feet = @square_feet,
                    net_acres = @acres{_auditColumns.BuildUpdateSetClause()}
                where loan_alias_id = @loan_alias_id
                """;

        private async Task<string?> TryResolveLoanStatusKeyColumnAsync()

        {

            if (!string.IsNullOrEmpty(_loanStatusKeyColumn))

            {

                return _loanStatusKeyColumn;

            }



            try

            {

                _loanStatusKeyColumn = await LoanDimStatusColumnResolver.ResolveAsync(

                    _connectionString,

                    _tblDimLoan);

                _logger.LogInformation(

                    "Using shared.dim_loan.{Column} for loan security value status filter.",

                    _loanStatusKeyColumn);

                return _loanStatusKeyColumn;

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Status filter skipped; shared.dim_loan status column unavailable.");

                return null;

            }

        }



        private string BuildListSql(

            IReadOnlyList<long>? loanAliasIds,

            LoanStatusFilter statusFilter,

            string? loanStatusKeyColumn)

        {

            var auditSelect = BuildAuditSelectColumns();



            var sql = new StringBuilder(

                $"""

                 select a.loan_alias_id,

                        a.loan_alias_name,

                        collateral = isnull(b.collateral, 0),

                        a.security_value,

                        a.units,

                        a.net_acres,

                        a.square_feet{auditSelect}

                 from {_tblLoanAliasMaster} a

                 left join (

                     select loan_alias_name,

                            sum(collateral) as collateral

                     from {_tblLoanAliasRelationship}

                     group by loan_alias_name

                 ) b on a.loan_alias_name = b.loan_alias_name

                 """);



            var whereClauses = new List<string>();

            if (loanAliasIds is { Count: > 0 })

            {

                whereClauses.Add(

                    "a.loan_alias_id in ("

                    + string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}"))

                    + ')');

            }



            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))

            {

                var existsSql = new StringBuilder();

                existsSql.Append("exists (");

                existsSql.Append($"select 1 from {_tblLoanAliasRelationship} rf");

                existsSql.Append(

                    $" inner join {_tblDimLoan} lf on {SubjectiveInputSql.EqualsVarchar("lf", "loan_code", "rf", "loan_code")} and {SubjectiveInputSql.DimLoanIsCurrent("lf")}");

                existsSql.Append(" where rf.loan_alias_name = a.loan_alias_name");

                LoanStatusFilterParser.AppendSqlCondition(

                    existsSql,

                    "lf",

                    loanStatusKeyColumn,

                    statusFilter,

                    _tblDimStatus);

                existsSql.Append(')');

                whereClauses.Add(existsSql.ToString());

            }



            if (whereClauses.Count > 0)

            {

                sql.Append(" where ");

                sql.Append(string.Join(" and ", whereClauses));

            }



            sql.AppendLine();

            sql.Append(" order by a.loan_alias_name");

            return sql.ToString();

        }



        private string BuildAuditSelectColumns()

        {

            var columns = _auditColumns.SelectListColumns()

                .Select(column => column.Trim('[', ']'))

                .ToList();

            return columns.Count == 0

                ? string.Empty

                : ",\n                        " + string.Join(",\n                        ", columns);

        }



        private async Task<IReadOnlyList<LoanSecurityValueDto>> ReadRowsAsync(SqlCommand command)

        {

            var rows = new List<LoanSecurityValueDto>();



            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())

            {

                rows.Add(MapRow(reader));

            }



            _logger.LogInformation("Retrieved {Count} loan security value rows.", rows.Count);

            return rows;

        }



        private static void AddFilterParameters(

            SqlCommand command,

            IReadOnlyList<long>? loanAliasIds,

            LoanStatusFilter statusFilter)

        {

            if (loanAliasIds is { Count: > 0 })

            {

                for (var i = 0; i < loanAliasIds.Count; i++)

                {

                    command.Parameters.AddWithValue($"@loan_alias_id_{i}", loanAliasIds[i]);

                }

            }



            LoanStatusFilterParser.AddParameters(command, statusFilter);

        }



        private LoanSecurityValueDto MapRow(SqlDataReader reader)

        {

            DateTime? ReadAuditDate(string? column)

            {

                if (string.IsNullOrWhiteSpace(column))

                {

                    return null;

                }



                if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))

                {

                    return null;

                }



                return DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);

            }



            string ReadAuditUser(string? column)

            {

                if (string.IsNullOrWhiteSpace(column))

                {

                    return string.Empty;

                }



                if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))

                {

                    return string.Empty;

                }



                return reader.GetString(ordinal);

            }



            return new LoanSecurityValueDto

            {

                LoanAliasId = reader.GetInt64OrDefault("loan_alias_id"),

                LoanAliasName = reader.GetStringOrEmpty("loan_alias_name"),

                CollateralPerYardi = reader.GetDecimalOrDefault("collateral"),

                SecurityValue = reader.GetNullableDecimal("security_value"),

                Units = reader.GetNullableInt32("units"),

                SquareFeet = reader.GetNullableDecimal("square_feet"),

                Acres = reader.GetNullableDecimal("net_acres"),

                UpdatedBy = ReadAuditUser(_auditColumns.ReadUpdatedByColumn),

                UpdatedDtm = ReadAuditDate(_auditColumns.ReadUpdatedDtmColumn)

            };

        }

    }

}


