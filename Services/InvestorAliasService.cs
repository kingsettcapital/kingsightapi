using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface IInvestorAliasService
    {
        Task<IReadOnlyList<InvestorAliasDto>> GetAllAsync();
        Task<InvestorAliasDto?> GetByIdAsync(long investorAliasId);
        Task<long> SaveAsync(InvestorAliasSaveRequest request);
        Task<bool> UpdateAsync(long investorAliasId, InvestorAliasUpdateRequest request);
        Task<bool> DeleteAsync(long investorAliasId);
    }

    public sealed class InvestorAliasService : IInvestorAliasService
    {
        private const string ListSql = """
            select investor_alias_id,
                   investor_alias_name,
                   created_by,
                   created_dtm,
                   updated_by,
                   updated_dtm
            from mort.investor_alias_master
            order by investor_alias_id
            """;

        private const string GetByIdSql = """
            select investor_alias_id,
                   investor_alias_name,
                   created_by,
                   created_dtm,
                   updated_by,
                   updated_dtm
            from mort.investor_alias_master
            where investor_alias_id = @investor_alias_id
            """;

        private const string NextIdSql = """
            select isnull(max(investor_alias_id), 0) + 1
            from mort.investor_alias_master
            """;

        private const string InsertSql = """
            insert into mort.investor_alias_master
                (investor_alias_id, investor_alias_name, created_by, created_dtm)
            values (@investor_alias_id, @investor_alias_name, @created_by, getutcdate())
            """;

        private const string UpdateSql = """
            update mort.investor_alias_master
            set investor_alias_name = @investor_alias_name,
                updated_by = @updated_by,
                updated_dtm = getutcdate()
            where investor_alias_id = @investor_alias_id
            """;

        private const string DeleteSql = """
            delete from mort.investor_alias_master
            where investor_alias_id = @investor_alias_id
            """;

        private readonly string _connectionString;
        private readonly ILogger<InvestorAliasService> _logger;

        public InvestorAliasService(IConfiguration configuration, ILogger<InvestorAliasService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
        }

        public async Task<IReadOnlyList<InvestorAliasDto>> GetAllAsync()
        {
            var rows = new List<InvestorAliasDto>();

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

            _logger.LogInformation("Retrieved {Count} investor alias rows.", rows.Count);
            return rows;
        }

        public async Task<InvestorAliasDto?> GetByIdAsync(long investorAliasId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(GetByIdSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@investor_alias_id", investorAliasId);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var ordinals = GetOrdinals(reader);
            return MapRow(reader, ordinals);
        }

        public async Task<long> SaveAsync(InvestorAliasSaveRequest request)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                var newId = await GetNextIdAsync(connection, transaction);

                await using var command = new SqlCommand(InsertSql, connection, transaction)
                {
                    CommandType = System.Data.CommandType.Text
                };
                command.Parameters.AddWithValue("@investor_alias_id", newId);
                command.Parameters.AddWithValue("@investor_alias_name", request.InvestorAliasName);
                command.Parameters.AddWithValue("@created_by", request.CreatedBy);

                await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Created investor alias row with id {InvestorAliasId}.", newId);
                return newId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task<long> GetNextIdAsync(SqlConnection connection, SqlTransaction transaction)
        {
            await using var command = new SqlCommand(NextIdSql, connection, transaction)
            {
                CommandType = System.Data.CommandType.Text
            };

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }

        public async Task<bool> UpdateAsync(long investorAliasId, InvestorAliasUpdateRequest request)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(UpdateSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@investor_alias_id", investorAliasId);
            command.Parameters.AddWithValue("@investor_alias_name", request.InvestorAliasName);
            command.Parameters.AddWithValue("@updated_by", request.UpdatedBy);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Updated investor alias row {InvestorAliasId}. Rows affected: {AffectedRows}",
                    investorAliasId,
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No row updated for investor_alias_id {InvestorAliasId}.", investorAliasId);
            return false;
        }

        public async Task<bool> DeleteAsync(long investorAliasId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(DeleteSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@investor_alias_id", investorAliasId);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Deleted investor alias row {InvestorAliasId}. Rows affected: {AffectedRows}",
                    investorAliasId,
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No row deleted for investor_alias_id {InvestorAliasId}.", investorAliasId);
            return false;
        }

        private static (int Id, int Name, int CreatedBy, int CreatedDtm, int UpdatedBy, int UpdatedDtm) GetOrdinals(SqlDataReader reader)
        {
            return (
                reader.GetOrdinal("investor_alias_id"),
                reader.GetOrdinal("investor_alias_name"),
                reader.GetOrdinal("created_by"),
                reader.GetOrdinal("created_dtm"),
                reader.GetOrdinal("updated_by"),
                reader.GetOrdinal("updated_dtm"));
        }

        private static InvestorAliasDto MapRow(
            SqlDataReader reader,
            (int Id, int Name, int CreatedBy, int CreatedDtm, int UpdatedBy, int UpdatedDtm) ordinals)
        {
            return new InvestorAliasDto
            {
                InvestorAliasId = reader.IsDBNull(ordinals.Id) ? 0L : Convert.ToInt64(reader.GetValue(ordinals.Id)),
                InvestorAliasName = reader.IsDBNull(ordinals.Name) ? string.Empty : reader.GetString(ordinals.Name),
                CreatedBy = reader.IsDBNull(ordinals.CreatedBy) ? string.Empty : reader.GetString(ordinals.CreatedBy),
                CreatedDtm = reader.IsDBNull(ordinals.CreatedDtm) ? null : reader.GetDateTime(ordinals.CreatedDtm),
                UpdatedBy = reader.IsDBNull(ordinals.UpdatedBy) ? string.Empty : reader.GetString(ordinals.UpdatedBy),
                UpdatedDtm = reader.IsDBNull(ordinals.UpdatedDtm) ? null : reader.GetDateTime(ordinals.UpdatedDtm)
            };
        }
    }
}
