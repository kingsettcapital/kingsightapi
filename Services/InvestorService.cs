using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IInvestorService
    {
        Task<IReadOnlyList<InvestorDto>> GetAllAsync();
        Task<bool> UpdateAsync(InvestorUpdateBatchRequest request);
    }

    public sealed class InvestorService : IInvestorService
    {
        private const string ListSql = """
            select a.investor_key,
                   a.investor_code,
                   a.investor_name,
                   a.investor_alias_key,
                   investor_alias_name=isNull(b.investor_alias_name, ''),
                   a.user_updated_by,
                   a.user_updated_date
            from mort.dim_investor a Left outer Join mort.investor_alias_master b
                on a.investor_alias_key = b.investor_alias_id
            where is_current = 1
            order by investor_code
            """;

        private const string UpdateSql = """
            update mort.dim_investor
            set investor_alias_key = @investor_alias_key,
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
                var dto = new InvestorDto
                {
                    InvestorKey = reader.IsDBNull(ordinals.Key)
                        ? 0L
                        : Convert.ToInt64(reader.GetValue(ordinals.Key)),
                    InvestorCode = reader.IsDBNull(ordinals.Code)
                        ? string.Empty
                        : reader.GetString(ordinals.Code),
                    InvestorName = reader.IsDBNull(ordinals.Name)
                        ? string.Empty
                        : reader.GetString(ordinals.Name),
                    InvestorAliasKey = reader.IsDBNull(ordinals.AliasKey)
                        ? (long?)null
                        : Convert.ToInt64(reader.GetValue(ordinals.AliasKey)),
                    InvestorAliasName = reader.IsDBNull(ordinals.AliasName)
                        ? string.Empty
                        : reader.GetString(ordinals.AliasName),
                    UserUpdatedBy = reader.IsDBNull(ordinals.UpdatedBy)
                        ? string.Empty
                        : reader.GetString(ordinals.UpdatedBy),
                    UserUpdatedDate = reader.IsDBNull(ordinals.UpdatedDate)
                        ? null
                        : reader.GetDateTime(ordinals.UpdatedDate)
                };

                rows.Add(dto);
            }

            _logger.LogInformation("Retrieved {Count} current investor rows.", rows.Count);
            return rows;
        }

        public async Task<bool> UpdateAsync(InvestorUpdateBatchRequest request)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var affectedRows = 0;
            foreach (var investor in request.Investors)
            {
                await using var command = new SqlCommand(UpdateSql, connection)
                {
                    CommandType = System.Data.CommandType.Text
                };
                command.Parameters.AddWithValue("@investor_key", investor.InvestorKey);
                command.Parameters.AddWithValue("@investor_alias_key", investor.InvestorAliasKey.HasValue ? (object)investor.InvestorAliasKey.Value : DBNull.Value);
                command.Parameters.AddWithValue("@user_updated_by", investor.UserUpdatedBy);

                affectedRows += await command.ExecuteNonQueryAsync();
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "investor Rows affected: {AffectedRows}",
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No row updated.");
            return false;
        }

        private static (int Key, int Code, int Name, int AliasKey, int AliasName, int UpdatedBy, int UpdatedDate) GetOrdinals(SqlDataReader reader)
        {
            return (
                reader.GetOrdinal("investor_key"),
                reader.GetOrdinal("investor_code"),
                reader.GetOrdinal("investor_name"),
                reader.GetOrdinal("investor_alias_key"),
                reader.GetOrdinal("investor_alias_name"),
                reader.GetOrdinal("user_updated_by"),
                reader.GetOrdinal("user_updated_date"));
        }
    }
}
