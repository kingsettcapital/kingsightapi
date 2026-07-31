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

        private readonly SubjectiveInputSql _sql;

        private readonly string _tblDimLoan;

        private readonly string _tblLoanAliasMaster;

        private readonly string _tblLoanAliasRelationship;

        private readonly string _tblDimStatus;

        private readonly string _tblYardiCollateralValue;

        private readonly string _tblYardiCollateralXref;

        private readonly string _tblYardiCollateral;

        private readonly string _tblYardiLookupValues;

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



            _sql = new SubjectiveInputSql(tables);

            _tblDimLoan = _sql.SharedDimLoan;

            _tblLoanAliasMaster = _sql.LoanAliasMaster;

            _tblLoanAliasRelationship = _sql.LoanAliasRelationship;

            _tblDimStatus = _sql.DimStatus;

            _tblYardiCollateralValue = tables.Yardi("Collateral_Value");

            _tblYardiCollateralXref = tables.Yardi("collateral_xref");

            _tblYardiCollateral = tables.Yardi("collateral");

            _tblYardiLookupValues = tables.Yardi("Lookup_Values");
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

                _logger.LogError(

                    ex,

                    "Loan security value query failed with status filter (column={Column}).",

                    loanStatusKeyColumn);

                throw;

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
                    where isnull(s.is_active, 1) = 1
                      and isnull(s.status_type, 'FUNDING') = 'FUNDING'
                    order by isnull(s.sort_order, 999999), s.status_name
                    """;



                await using var command = new SqlCommand(statusOptionsSql, connection);

                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())

                {

                    var statusKey = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0));

                    var statusName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);

                    if (statusKey < 0 || string.IsNullOrWhiteSpace(statusName))

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
            await _sql.EnsureDimLoanCurrentIndicatorAsync(_connectionString);
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

                 with latest_collateral_value as (

                     select collateral_id, valuation_date, collateral_amount

                     from (

                         select collateral_id, valuation_date, collateral_amount,

                                row_number() over (partition by collateral_id order by valuation_date desc) as rn

                         from {_tblYardiCollateralValue}

                     ) t

                     where rn = 1

                 ),

                 dataset as (

                     select e.loan_alias_name,

                            e.loan_code,

                            col.collateral_name,

                            cv.collateral_amount,

                            row_number() over (

                                partition by e.loan_alias_name, col.collateral_name

                                order by e.loan_alias_name, col.collateral_name

                            ) as rn

                     from {_tblLoanAliasRelationship} e

                     inner join {_tblDimLoan} f

                         on {SubjectiveInputSql.EqualsVarchar("e", "loan_code", "f", "loan_code")}

                     inner join {_tblLoanAliasMaster} g

                         on e.loan_alias_name = g.loan_alias_name

                     left join (

                         select distinct collateral_id, loan_id

                         from {_tblYardiCollateralXref}

                     ) xref on xref.loan_id = f.loan_id

                     left join {_tblYardiCollateral} col

                         on xref.collateral_id = col.collateral_id

                     inner join {_tblYardiLookupValues} lv

                         on col.collateral_type = lv.lookup_sk

                        and lv.lookup_value = 'Real Estate'

                     left join latest_collateral_value cv

                         on col.collateral_id = cv.collateral_id

                 ),

                 dataset2 as (

                     select loan_alias_name,

                            sum(collateral_amount) as collateral

                     from dataset

                     where rn = 1

                     group by loan_alias_name

                 )

                 select a.loan_alias_id,

                        a.loan_alias_name,

                        collateral = isnull(b.collateral, 0),

                        security_value = coalesce(nullif(a.security_value, 0), b.collateral),

                        a.units,

                        a.net_acres,

                        a.square_feet{auditSelect}

                 from {_tblLoanAliasMaster} a

                 left join dataset2 b

                     on a.loan_alias_name = b.loan_alias_name

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

                    $" inner join {_tblDimLoan} lf on {SubjectiveInputSql.EqualsVarchar("lf", "loan_code", "rf", "loan_code")} and {_sql.DimLoanIsCurrent("lf")}");

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


