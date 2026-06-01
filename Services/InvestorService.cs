using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IInvestorService
    {
        Task<IReadOnlyList<InvestorDto>> GetAllAsync();
        Task<bool> UpdateAsync(long investorKey, InvestorUpdateRequest request);
    }

    public sealed class InvestorService : IInvestorService
    {
        private const string ListSql = """
            select investor_key,
                   investor_code,
                   investor_name,
                   investor_alias_name
            from mort.dim_investor
            where is_current = 1
            order by investor_code
            """;

        private const string UpdateSql = """
            update mort.dim_investor
            set investor_alias_name = @investor_alias_name,
                user_updated_by = @user_updated_by,
                user_updated_date = getutcdate()
            where investor_key = @investor_key
              and is_current = 1
            """;

        private readonly string _connectionString;
        private readonly ILogger<InvestorService> _logger;

        public InvestorService(IConfiguration configuration, ILogger<InvestorService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
        }

        public async Task<IReadOnlyList<InvestorDto>> GetAllAsync()
        {
            var rows = new List<InvestorDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(ListSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            await using var reader = await command.ExecuteReaderAsync();
            var ordinals = GetOrdinals(reader);

            while (await reader.ReadAsync())
            {
                rows.Add(MapRow(reader, ordinals));
            }

            _logger.LogInformation("Retrieved {Count} current investor rows.", rows.Count);
            return rows;
        }

        public async Task<bool> UpdateAsync(long investorKey, InvestorUpdateRequest request)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(UpdateSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@investor_key", investorKey);
            command.Parameters.AddWithValue("@investor_alias_name", request.InvestorAliasName);
            command.Parameters.AddWithValue("@user_updated_by", request.UserUpdatedBy);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Updated investor row {InvestorKey}. Rows affected: {AffectedRows}",
                    investorKey,
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No row updated for investor_key {InvestorKey}.", investorKey);
            return false;
        }

        private static (int Key, int Code, int Name, int AliasName) GetOrdinals(SqlDataReader reader)
        {
            return (
                reader.GetOrdinal("investor_key"),
                reader.GetOrdinal("investor_code"),
                reader.GetOrdinal("investor_name"),
                reader.GetOrdinal("investor_alias_name"));
        }

        private static InvestorDto MapRow(
            SqlDataReader reader,
            (int Key, int Code, int Name, int AliasName) ordinals)
        {
            return new InvestorDto
            {
                InvestorKey = reader.IsDBNull(ordinals.Key) ? 0L : Convert.ToInt64(reader.GetValue(ordinals.Key)),
                InvestorCode = reader.IsDBNull(ordinals.Code) ? string.Empty : reader.GetString(ordinals.Code),
                InvestorName = reader.IsDBNull(ordinals.Name) ? string.Empty : reader.GetString(ordinals.Name),
                InvestorAliasName = reader.IsDBNull(ordinals.AliasName) ? string.Empty : reader.GetString(ordinals.AliasName)
            };
        }
    }
}
