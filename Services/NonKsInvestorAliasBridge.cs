using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    /// <summary>
    /// Keeps Non-KS <c>external_serviced_loan</c> investor codes in sync with
    /// <c>investor_alias_relationship</c> so they appear on Investor Alias Assignment.
    /// Assigned aliases are read back on Non-KS via the relationship list (by investor_code).
    /// </summary>
    public interface INonKsInvestorAliasBridge
    {
        Task EnsureMissingRelationshipRowsAsync(
            SqlConnection connection,
            CancellationToken cancellationToken = default);

        Task EnsureRelationshipRowAsync(
            SqlConnection connection,
            string investorCode,
            string? investorName,
            string? investorAliasName,
            string auditDisplayName,
            CancellationToken cancellationToken = default);
    }

    public sealed class NonKsInvestorAliasBridge : INonKsInvestorAliasBridge
    {
        private readonly string _connectionString;
        private readonly SubjectiveInputSql _sql;
        private readonly ILogger<NonKsInvestorAliasBridge> _logger;

        private bool _schemaProbed;
        private SubjectiveInputRelationshipAuditColumns _relationshipAudit = new();
        private string? _eslInvestorCodeColumn;
        private string? _eslInvestorTextColumn;

        public NonKsInvestorAliasBridge(
            IConfiguration configuration,
            FabricWarehouseTables tables,
            ILogger<NonKsInvestorAliasBridge> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _sql = new SubjectiveInputSql(tables);
            _logger = logger;
        }

        public async Task EnsureMissingRelationshipRowsAsync(
            SqlConnection connection,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);
            if (_eslInvestorCodeColumn is null)
            {
                return;
            }

            var missing = await ListMissingNonKsInvestorsAsync(connection, cancellationToken);
            if (missing.Count == 0)
            {
                return;
            }

            foreach (var row in missing)
            {
                await InsertRelationshipRowIfMissingAsync(
                    connection,
                    row.InvestorCode,
                    row.InvestorName,
                    null,
                    "system",
                    cancellationToken);
            }

            _logger.LogInformation(
                "Synced {Count} Non-KS investor(s) into investor_alias_relationship for Assignment.",
                missing.Count);
        }

        public async Task EnsureRelationshipRowAsync(
            SqlConnection connection,
            string investorCode,
            string? investorName,
            string? investorAliasName,
            string auditDisplayName,
            CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);
            var code = investorCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            await InsertRelationshipRowIfMissingAsync(
                connection,
                code,
                investorName,
                investorAliasName,
                string.IsNullOrWhiteSpace(auditDisplayName) ? "system" : auditDisplayName.Trim(),
                cancellationToken);
        }

        private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            if (_schemaProbed)
            {
                return;
            }

            _relationshipAudit = await SubjectiveInputRelationshipAuditColumns.ProbeAsync(
                _connectionString,
                _sql.InvestorAliasRelationship,
                cancellationToken);

            _eslInvestorCodeColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.ExternalServicedLoan,
                ["investor_code"],
                cancellationToken);
            _eslInvestorTextColumn = await DimLoanColumnProbe.FindFirstAsync(
                _connectionString,
                _sql.ExternalServicedLoan,
                ["investor_name", "investor", "investor_alias_name"],
                cancellationToken);

            _schemaProbed = true;
        }

        private async Task<IReadOnlyList<MissingNonKsInvestor>> ListMissingNonKsInvestorsAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            var nameExpr = _eslInvestorTextColumn is null
                ? "cast('' as varchar(200))"
                : $"isnull(max(cast(e.[{_eslInvestorTextColumn}] as varchar(200))), '')";

            var sql = $"""
                select
                    investor_code = cast(e.[{_eslInvestorCodeColumn}] as varchar(100)),
                    investor_name = {nameExpr}
                from {_sql.ExternalServicedLoan} e
                where e.[{_eslInvestorCodeColumn}] is not null
                  and ltrim(rtrim(cast(e.[{_eslInvestorCodeColumn}] as varchar(100)))) <> ''
                  and not exists (
                      select 1
                      from {_sql.InvestorAliasRelationship} r
                      where cast(r.investor_code as varchar(100)) collate database_default
                          = cast(e.[{_eslInvestorCodeColumn}] as varchar(100)) collate database_default
                  )
                group by cast(e.[{_eslInvestorCodeColumn}] as varchar(100))
                """;

            var rows = new List<MissingNonKsInvestor>();
            await using var command = new SqlCommand(sql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0));
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                rows.Add(new MissingNonKsInvestor(
                    code.Trim(),
                    reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1))));
            }

            return rows;
        }

        private async Task InsertRelationshipRowIfMissingAsync(
            SqlConnection connection,
            string investorCode,
            string? investorName,
            string? investorAliasName,
            string auditDisplayName,
            CancellationToken cancellationToken)
        {
            var existsSql = $"""
                select top 1 1
                from {_sql.InvestorAliasRelationship}
                where cast(investor_code as varchar(100)) collate database_default
                    = cast(@investor_code as varchar(100)) collate database_default
                """;

            await using (var existsCommand = new SqlCommand(existsSql, connection))
            {
                existsCommand.Parameters.AddWithValue("@investor_code", investorCode);
                var exists = await existsCommand.ExecuteScalarAsync(cancellationToken);
                if (exists is not null && exists != DBNull.Value)
                {
                    return;
                }
            }

            var (auditColumns, auditValues) = _relationshipAudit.BuildInsertColumnList();
            var columns = new List<string> { "investor_code", "investor_name", "investor_alias_name" };
            var values = new List<string> { "@investor_code", "@investor_name", "@investor_alias_name" };
            columns.AddRange(auditColumns);
            values.AddRange(auditValues);

            var insertSql = $"""
                insert into {_sql.InvestorAliasRelationship} (
                    {string.Join(", ", columns)})
                values (
                    {string.Join(", ", values)})
                """;

            await using var insertCommand = new SqlCommand(insertSql, connection);
            insertCommand.Parameters.AddWithValue("@investor_code", investorCode);
            insertCommand.Parameters.AddWithValue(
                "@investor_name",
                string.IsNullOrWhiteSpace(investorName) ? investorCode : investorName.Trim());
            insertCommand.Parameters.AddWithValue(
                "@investor_alias_name",
                string.IsNullOrWhiteSpace(investorAliasName) ? DBNull.Value : investorAliasName.Trim());
            _relationshipAudit.AddUpdateParameters(insertCommand, auditDisplayName, DateTime.UtcNow);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        private sealed record MissingNonKsInvestor(string InvestorCode, string? InvestorName);
    }
}
