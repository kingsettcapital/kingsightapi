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
        private readonly string _collateralSubqueryBase;
        private readonly string _listSqlBase;
        private readonly string _listSqlFallback;
        private readonly string _listSqlJoinEnd;
        private readonly string _updateSql;
        private readonly string _updateSqlFallback;
        private readonly string _statusOptionsSql;

        private readonly string _connectionString;
        private readonly FabricWarehouseTables _tables;
        private readonly string _tblDimLoan;
        private readonly string _tblLoanAliasMaster;
        private readonly string _tblDimStatus;
        private readonly ILogger<LoanSecurityValueService> _logger;
        private bool? _extendedColumnsAvailable;
        private string? _loanStatusKeyColumn;

        public LoanSecurityValueService(
            IConfiguration configuration,
            ILogger<LoanSecurityValueService> logger,
            FabricWarehouseTables tables)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _tables = tables;
            _tblDimLoan = tables.Mort("dim_loan");
            _tblLoanAliasMaster = tables.Mort("loan_alias_master");
            _tblDimStatus = tables.Mort("dim_status");

            _collateralSubqueryBase = $"""
                select l.loan_alias_key,
                       sum(isnull(l.collateral, 0)) as collateral_per_yardi
                from {_tblDimLoan} l
                where l.is_current = 1
                  and l.loan_alias_key is not null
                """;

            _listSqlBase = $"""
                select m.loan_alias_id,
                       m.loan_alias_name,
                       isnull(c.collateral_per_yardi, 0) as collateral_per_yardi,
                       m.security_value,
                       m.units,
                       m.square_feet,
                       m.acres,
                       isnull(m.updated_by, '') as updated_by,
                       m.updated_dtm
                from {_tblLoanAliasMaster} m
                left join (
                """;

            _listSqlFallback = $"""
                select m.loan_alias_id,
                       m.loan_alias_name,
                       isnull(c.collateral_per_yardi, 0) as collateral_per_yardi,
                       m.security_value,
                       isnull(m.updated_by, '') as updated_by,
                       m.updated_dtm
                from {_tblLoanAliasMaster} m
                left join (
                """;

            _listSqlJoinEnd = """
                ) c on m.loan_alias_id = c.loan_alias_key
                """;

            _updateSql = $"""
                update {_tblLoanAliasMaster}
                set security_value = @security_value,
                    units = @units,
                    square_feet = @square_feet,
                    acres = @acres,
                    updated_by = @updated_by,
                    updated_dtm = getutcdate()
                where loan_alias_id = @loan_alias_id
                """;

            _updateSqlFallback = $"""
                update {_tblLoanAliasMaster}
                set security_value = @security_value,
                    updated_by = @updated_by,
                    updated_dtm = getutcdate()
                where loan_alias_id = @loan_alias_id
                """;

            _statusOptionsSql = $"""
                select s.status_key,
                       s.status_name
                from {_tblDimStatus} s
                order by s.status_name
                """;
        }

        public async Task<IReadOnlyList<LoanSecurityValueDto>> GetAllAsync(
            IReadOnlyList<long>? loanAliasIds = null,
            IReadOnlyList<string>? statuses = null)
        {
            var useExtendedColumns = await GetExtendedColumnsAvailableAsync();
            var statusFilter = LoanStatusFilterParser.Parse(statuses);
            var loanStatusKeyColumn = statusFilter.HasFilter
                ? await GetLoanStatusKeyColumnAsync()
                : null;
            var sql = BuildListSql(loanAliasIds, statusFilter, useExtendedColumns, loanStatusKeyColumn);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            AddFilterParameters(command, loanAliasIds, statusFilter);

            try
            {
                return await ReadRowsAsync(command, useExtendedColumns);
            }
            catch (SqlException ex) when (IsMissingOptionalColumnError(ex) && useExtendedColumns)
            {
                _extendedColumnsAvailable = false;
                _logger.LogWarning(
                    "loan_alias_master is missing units/square_feet/acres; retrying without those columns.");
                return await GetAllAsync(loanAliasIds, statuses);
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

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(_statusOptionsSql, connection);
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

            _logger.LogInformation("Retrieved {Count} loan security value status options from dim_status.", options.Count);
            return options;
        }

        public async Task<bool> UpdateAsync(LoanSecurityValueBatchUpdateRequest request, string auditDisplayName)
        {
            var useExtendedColumns = await GetExtendedColumnsAvailableAsync();
            var updateSql = useExtendedColumns ? _updateSql : _updateSqlFallback;

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
                command.Parameters.AddWithValue("@updated_by", auditDisplayName);

                if (useExtendedColumns)
                {
                    command.Parameters.AddWithValue(
                        "@units",
                        item.Units.HasValue ? item.Units.Value : DBNull.Value);
                    command.Parameters.AddWithValue(
                        "@square_feet",
                        item.SquareFeet.HasValue ? item.SquareFeet.Value : DBNull.Value);
                    command.Parameters.AddWithValue(
                        "@acres",
                        item.Acres.HasValue ? item.Acres.Value : DBNull.Value);
                }

                try
                {
                    affectedRows += await command.ExecuteNonQueryAsync();
                }
                catch (SqlException ex) when (IsMissingOptionalColumnError(ex) && useExtendedColumns)
                {
                    _extendedColumnsAvailable = false;
                    _logger.LogWarning(
                        "loan_alias_master is missing units/square_feet/acres; retrying update without those columns.");
                    return await UpdateAsync(request, auditDisplayName);
                }
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Updated {AffectedRows} loan security value rows.", affectedRows);
                return true;
            }

            _logger.LogWarning("No loan security value rows updated.");
            return false;
        }

        private async Task<string> GetLoanStatusKeyColumnAsync()
        {
            if (!string.IsNullOrEmpty(_loanStatusKeyColumn))
            {
                return _loanStatusKeyColumn;
            }

            _loanStatusKeyColumn = await LoanDimStatusColumnResolver.ResolveAsync(
                _connectionString,
                _tblDimLoan);

            _logger.LogInformation(
                "Using mort.dim_loan.{Column} for loan status filter.",
                _loanStatusKeyColumn);

            return _loanStatusKeyColumn;
        }

        private async Task<bool> GetExtendedColumnsAvailableAsync()
        {
            if (_extendedColumnsAvailable.HasValue)
            {
                return _extendedColumnsAvailable.Value;
            }

            var probeSql = $"""
                select top 0 units, square_feet, acres
                from {_tblLoanAliasMaster}
                """;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                await using var command = new SqlCommand(probeSql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                _extendedColumnsAvailable = true;
            }
            catch (SqlException ex) when (IsMissingOptionalColumnError(ex))
            {
                _extendedColumnsAvailable = false;
            }

            return _extendedColumnsAvailable.Value;
        }

        private async Task<IReadOnlyList<LoanSecurityValueDto>> ReadRowsAsync(
            SqlCommand command,
            bool useExtendedColumns)
        {
            var rows = new List<LoanSecurityValueDto>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(MapRow(reader, useExtendedColumns));
            }

            _logger.LogInformation("Retrieved {Count} loan security value rows.", rows.Count);
            return rows;
        }

        private string BuildListSql(
            IReadOnlyList<long>? loanAliasIds,
            LoanStatusFilter statusFilter,
            bool useExtendedColumns,
            string? loanStatusKeyColumn)
        {
            var collateralSql = new StringBuilder(_collateralSubqueryBase);
            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                LoanStatusFilterParser.AppendSqlCondition(
                    collateralSql,
                    "l",
                    loanStatusKeyColumn,
                    statusFilter,
                    _tblDimStatus);
            }

            collateralSql.AppendLine();
            collateralSql.Append(" group by l.loan_alias_key");

            var sql = new StringBuilder(useExtendedColumns ? _listSqlBase : _listSqlFallback);
            sql.Append(collateralSql);
            sql.Append(_listSqlJoinEnd);

            var whereClauses = new List<string>();
            if (loanAliasIds is { Count: > 0 })
            {
                whereClauses.Add(
                    "m.loan_alias_id in ("
                    + string.Join(", ", loanAliasIds.Select((_, i) => $"@loan_alias_id_{i}"))
                    + ')');
            }

            if (statusFilter.HasFilter && !string.IsNullOrEmpty(loanStatusKeyColumn))
            {
                var existsSql = new StringBuilder();
                existsSql.Append("exists (");
                existsSql.Append($"select 1 from {_tblDimLoan} lf");
                existsSql.Append(" where lf.is_current = 1");
                existsSql.Append(" and lf.loan_alias_key = m.loan_alias_id");
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
            sql.Append(" order by m.loan_alias_name");
            return sql.ToString();
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

        private static LoanSecurityValueDto MapRow(SqlDataReader reader, bool useExtendedColumns)
        {
            return new LoanSecurityValueDto
            {
                LoanAliasId = GetInt64(reader, "loan_alias_id"),
                LoanAliasName = GetString(reader, "loan_alias_name"),
                CollateralPerYardi = GetDecimal(reader, "collateral_per_yardi") ?? 0m,
                SecurityValue = GetDecimal(reader, "security_value"),
                Units = useExtendedColumns ? GetInt32(reader, "units") : null,
                SquareFeet = useExtendedColumns ? GetDecimal(reader, "square_feet") : null,
                Acres = useExtendedColumns ? GetDecimal(reader, "acres") : null,
                UpdatedBy = GetString(reader, "updated_by"),
                UpdatedDtm = GetDateTime(reader, "updated_dtm")
            };
        }

        private static bool IsMissingOptionalColumnError(SqlException ex) =>
            ex.Number == 207 && (
                ex.Message.Contains("units", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("square_feet", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("acres", StringComparison.OrdinalIgnoreCase));

        private static int? FindOrdinal(SqlDataReader reader, string columnName)
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return null;
        }

        private static long GetInt64(SqlDataReader reader, string columnName)
        {
            var ordinal = FindOrdinal(reader, columnName);
            return ordinal is null || reader.IsDBNull(ordinal.Value)
                ? 0L
                : Convert.ToInt64(reader.GetValue(ordinal.Value));
        }

        private static string GetString(SqlDataReader reader, string columnName)
        {
            var ordinal = FindOrdinal(reader, columnName);
            return ordinal is null || reader.IsDBNull(ordinal.Value)
                ? string.Empty
                : reader.GetString(ordinal.Value);
        }

        private static decimal? GetDecimal(SqlDataReader reader, string columnName)
        {
            var ordinal = FindOrdinal(reader, columnName);
            return ordinal is null || reader.IsDBNull(ordinal.Value)
                ? null
                : Convert.ToDecimal(reader.GetValue(ordinal.Value));
        }

        private static int? GetInt32(SqlDataReader reader, string columnName)
        {
            var ordinal = FindOrdinal(reader, columnName);
            return ordinal is null || reader.IsDBNull(ordinal.Value)
                ? null
                : Convert.ToInt32(reader.GetValue(ordinal.Value));
        }

        private static DateTime? GetDateTime(SqlDataReader reader, string columnName)
        {
            var ordinal = FindOrdinal(reader, columnName);
            return ordinal is null || reader.IsDBNull(ordinal.Value)
                ? null
                : reader.GetDateTime(ordinal.Value);
        }
    }
}
