using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace kingsightapi.Services
{
    public interface IInvestorService
    {
        Task<IReadOnlyList<InvestorDto>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<InvestorDto> CreateAsync(
            InvestorCreateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            InvestorUpdateBatchRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default);
    }

    public sealed class InvestorService : IInvestorService
    {
        private const string ExtInvestorCodePrefix = "NKSInv-";

        private readonly string _connectionString;
        private readonly SubjectiveInputSql _sql;
        private readonly ILogger<InvestorService> _logger;

        private bool _schemaProbed;
        private SubjectiveInputRelationshipAuditColumns _auditColumns = new();

        public InvestorService(IConfiguration configuration, FabricWarehouseTables tables, ILogger<InvestorService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
            _sql = new SubjectiveInputSql(tables);
        }

        public async Task<IReadOnlyList<InvestorDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            var rows = new List<InvestorDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(BuildListSql(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(MapRow(reader));
            }

            _logger.LogInformation("Retrieved {Count} investor alias relationship rows.", rows.Count);
            return rows;
        }

        public async Task<InvestorDto> CreateAsync(
            InvestorCreateRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            var investorName = request.InvestorName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(investorName))
            {
                throw new InvalidOperationException("Investor name is required.");
            }

            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var investorCode = await GetNextExtInvestorCodeAsync(connection, cancellationToken);
            var auditUtc = DateTime.UtcNow;

            await using var command = new SqlCommand(BuildInsertSql(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@investor_code", investorCode);
            command.Parameters.AddWithValue("@investor_name", investorName);
            _auditColumns.AddUpdateParameters(command, auditDisplayName, auditUtc);

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Created investor {InvestorCode} ({InvestorName}) by {AuditUser}.",
                investorCode,
                investorName,
                auditDisplayName);

            return new InvestorDto
            {
                InvestorKey = 0,
                InvestorCode = investorCode,
                InvestorName = investorName,
                InvestorAliasKey = null,
                InvestorAliasName = string.Empty,
                UserUpdatedBy = auditDisplayName,
                UserUpdatedDate = auditUtc
            };
        }

        public async Task<bool> UpdateAsync(
            InvestorUpdateBatchRequest request,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var affectedRows = 0;
            foreach (var investor in request.Investors)
            {
                if (!investor.InvestorAliasKey.HasValue || string.IsNullOrWhiteSpace(investor.InvestorCode))
                {
                    continue;
                }

                await using var command = new SqlCommand(BuildUpdateSql(), connection)
                {
                    CommandType = System.Data.CommandType.Text
                };
                command.Parameters.AddWithValue("@investor_code", investor.InvestorCode.Trim());
                command.Parameters.AddWithValue("@investor_alias_key", investor.InvestorAliasKey.Value);
                _auditColumns.AddUpdateParameters(command, auditDisplayName, DateTime.UtcNow);

                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Investor alias relationship rows affected: {AffectedRows} by {AuditUser}",
                    affectedRows,
                    auditDisplayName);
                return true;
            }

            _logger.LogWarning("No investor relationship rows updated.");
            return false;
        }

        private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            if (_schemaProbed)
            {
                return;
            }

            _auditColumns = await SubjectiveInputRelationshipAuditColumns.ProbeAsync(
                _connectionString,
                _sql.InvestorAliasRelationship,
                cancellationToken);
            _schemaProbed = true;
        }

        private string BuildListSql() =>
            $"""
                select investor_key = 0,
                       r.investor_code,
                       r.investor_name,
                       investor_alias_key = m.investor_alias_id,
                       investor_alias_name = isnull(r.investor_alias_name, ''),
                       user_updated_by = {_auditColumns.BuildSelectUpdatedByExpression()},
                       user_updated_date = {_auditColumns.BuildSelectUpdatedDtmExpression()}
                from {_sql.InvestorAliasRelationship} r
                {_sql.InvestorAliasMasterJoinOnName()}
                order by r.investor_code
                """;

        private string BuildInsertSql()
        {
            var (auditColumns, auditValues) = _auditColumns.BuildInsertColumnList();
            var columns = new List<string> { "investor_code", "investor_name", "investor_alias_name" };
            var values = new List<string> { "@investor_code", "@investor_name", "null" };
            columns.AddRange(auditColumns);
            values.AddRange(auditValues);

            return $"""
                insert into {_sql.InvestorAliasRelationship} (
                    {string.Join(", ", columns)})
                values (
                    {string.Join(", ", values)})
                """;
        }

        private string BuildUpdateSql() =>
            $"""
                update r
                set investor_alias_name = m.investor_alias_name{_auditColumns.BuildUpdateSetClause()}
                from {_sql.InvestorAliasRelationship} r
                inner join {_sql.InvestorAliasMaster} m
                    on m.investor_alias_id = @investor_alias_key
                where cast(r.investor_code as varchar(100)) collate database_default = cast(@investor_code as varchar(100)) collate database_default
                """;

        private async Task<string> GetNextExtInvestorCodeAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                select investor_code
                from {_sql.InvestorAliasRelationship}
                where investor_code like @prefixNew
                   or investor_code like @prefixLegacy
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@prefixNew", $"{ExtInvestorCodePrefix}%");
            command.Parameters.AddWithValue("@prefixLegacy", "NONKSInv-%");

            var maxNumber = 0;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0));
                var number = ParseExtInvestorCodeNumber(code);
                if (number > maxNumber)
                {
                    maxNumber = number;
                }
            }

            return $"{ExtInvestorCodePrefix}{maxNumber + 1}";
        }

        private static int ParseExtInvestorCodeNumber(string? investorCode)
        {
            if (string.IsNullOrWhiteSpace(investorCode))
            {
                return 0;
            }

            var match = Regex.Match(
                investorCode.Trim(),
                @"^(?:NKSInv|NONKSInv)-(\d+)$",
                RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out var parsed) ? parsed : 0;
        }

        private InvestorDto MapRow(SqlDataReader reader)
        {
            DateTime? updatedDate = null;
            if (reader.TryGetOrdinal("user_updated_date", out var dateOrd) && !reader.IsDBNull(dateOrd))
            {
                updatedDate = DateTime.SpecifyKind(reader.GetDateTime(dateOrd), DateTimeKind.Utc);
            }

            return new InvestorDto
            {
                InvestorKey = reader.GetInt64OrDefault("investor_key"),
                InvestorCode = reader.GetStringOrEmpty("investor_code"),
                InvestorName = reader.GetStringOrEmpty("investor_name"),
                InvestorAliasKey = reader.TryGetOrdinal("investor_alias_key", out var aliasKeyOrd) && !reader.IsDBNull(aliasKeyOrd)
                    ? Convert.ToInt64(reader.GetValue(aliasKeyOrd))
                    : null,
                InvestorAliasName = reader.GetStringOrEmpty("investor_alias_name"),
                UserUpdatedBy = reader.GetStringOrEmpty("user_updated_by"),
                UserUpdatedDate = updatedDate
            };
        }
    }
}
