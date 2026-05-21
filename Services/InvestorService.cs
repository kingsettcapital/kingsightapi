using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IInvestorService
    {
        Task<IReadOnlyList<DimInvestorDto>> GetInvestorsAsync();
        Task<DimInvestorAliasBatchUpdateResult> UpdateInvestorAliasesAsync(DimInvestorAliasBatchUpdateRequest request);
    }

    public sealed class InvestorService : IInvestorService
    {
        private const string ListInvestorsSql = """
            select investor_key,
                   investor_code,
                   investor_name,
                   investor_alias_name,
                   user_updated_date,
                   user_updated_by,
                   is_current,
                   effective_start_date,
                   effective_end_date
            from mort.dim_investor
            Where is_current = 1
            order by investor_key
            """;

        private const string UpdateInvestorAliasSql = """
            update mort.dim_investor
            set investor_alias_name = @investor_alias_name,
                user_updated_date = getutcdate(),
                user_updated_by = 'System'
            where investor_key = @investor_key
            """;

        private readonly string _connectionString;
        private readonly ILogger<InvestorService> _logger;

        public InvestorService(IConfiguration configuration, ILogger<InvestorService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
        }

        public async Task<IReadOnlyList<DimInvestorDto>> GetInvestorsAsync()
        {
            var rows = new List<DimInvestorDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(ListInvestorsSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            await using var reader = await command.ExecuteReaderAsync();

            var investorKeyOrdinal = reader.GetOrdinal("investor_key");
            var investorCodeOrdinal = reader.GetOrdinal("investor_code");
            var investorNameOrdinal = reader.GetOrdinal("investor_name");
            var investorAliasNameOrdinal = reader.GetOrdinal("investor_alias_name");
            var userUpdatedDateOrdinal = reader.GetOrdinal("user_updated_date");
            var userUpdatedByOrdinal = reader.GetOrdinal("user_updated_by");

            while (await reader.ReadAsync())
            {
                rows.Add(new DimInvestorDto
                {
                    investor_key = reader.IsDBNull(investorKeyOrdinal) ? 0L : Convert.ToInt64(reader.GetValue(investorKeyOrdinal)),
                    investor_code = reader.IsDBNull(investorCodeOrdinal) ? string.Empty : reader.GetString(investorCodeOrdinal),
                    investor_name = reader.IsDBNull(investorNameOrdinal) ? string.Empty : reader.GetString(investorNameOrdinal),
                    investor_alias_name = reader.IsDBNull(investorAliasNameOrdinal) ? string.Empty : reader.GetString(investorAliasNameOrdinal),
                    user_updated_date = reader.IsDBNull(userUpdatedDateOrdinal)? null : reader.GetDateTime(userUpdatedDateOrdinal),
                    user_updated_by = reader.IsDBNull(userUpdatedByOrdinal) ? string.Empty : reader.GetString(userUpdatedByOrdinal)
                });
            }

            _logger.LogInformation("Retrieved {Count} investor rows from mort.dim_investor.", rows.Count);
            return rows;
        }

        public async Task<DimInvestorAliasBatchUpdateResult> UpdateInvestorAliasesAsync(DimInvestorAliasBatchUpdateRequest request)
        {
            var notFound = new List<long>();
            var updated = 0;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(UpdateInvestorAliasSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            var keyParam = command.Parameters.Add("@investor_key", System.Data.SqlDbType.BigInt);
            var aliasParam = command.Parameters.Add("@investor_alias_name", System.Data.SqlDbType.NVarChar);

            foreach (var item in request.Investors)
            {
                keyParam.Value = item.investor_key;
                aliasParam.Value = item.investor_alias_name;

                var affectedRows = await command.ExecuteNonQueryAsync();
                if (affectedRows > 0)
                {
                    updated++;
                    _logger.LogInformation(
                        "Updated investor_alias_name for investor_key {InvestorKey}. Rows affected: {AffectedRows}",
                        item.investor_key, affectedRows);
                }
                else
                {
                    notFound.Add(item.investor_key);
                    _logger.LogWarning("No row updated for investor_key {InvestorKey} (not found or unchanged).", item.investor_key);
                }
            }

            return new DimInvestorAliasBatchUpdateResult
            {
                UpdatedCount = updated,
                NotFoundInvestorKeys = notFound
            };
        }
    }
}
