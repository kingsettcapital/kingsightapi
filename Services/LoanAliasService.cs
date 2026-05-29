using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface ILoanAliasService
    {
        Task<IReadOnlyList<LoanAliasDto>> GetAllAsync();
        Task<LoanAliasDto?> GetByIdAsync(long loanAliasId);
        Task<long> SaveAsync(LoanAliasSaveRequest request);
        Task<bool> UpdateAsync(long loanAliasId, LoanAliasUpdateRequest request);
        Task<bool> DeleteAsync(long loanAliasId);
    }

    public sealed class LoanAliasService : ILoanAliasService
    {
        private const string ListSql = """
            select loan_alias_id,
                   loan_alias_name,
                   created_by,
                   created_dtm,
                   updated_by,
                   updated_dtm
            from mort.loan_alias_master
            order by loan_alias_id
            """;

        private const string GetByIdSql = """
            select loan_alias_id,
                   loan_alias_name,
                   created_by,
                   created_dtm,
                   updated_by,
                   updated_dtm
            from mort.loan_alias_master
            where loan_alias_id = @loan_alias_id
            """;

        private const string NextIdSql = """
            select isnull(max(loan_alias_id), 0) + 1
            from mort.loan_alias_master
            """;

        private const string InsertSql = """
            insert into mort.loan_alias_master
                (loan_alias_id, loan_alias_name, created_by, created_dtm)
            values (@loan_alias_id, @loan_alias_name, @created_by, getutcdate())
            """;

        private const string UpdateSql = """
            update mort.loan_alias_master
            set loan_alias_name = @loan_alias_name,
                updated_by = @updated_by,
                updated_dtm = getutcdate()
            where loan_alias_id = @loan_alias_id
            """;

        private const string DeleteSql = """
            delete from mort.loan_alias_master
            where loan_alias_id = @loan_alias_id
            """;

        private readonly string _connectionString;
        private readonly ILogger<LoanAliasService> _logger;

        public LoanAliasService(IConfiguration configuration, ILogger<LoanAliasService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
        }

        public async Task<IReadOnlyList<LoanAliasDto>> GetAllAsync()
        {
            var rows = new List<LoanAliasDto>();

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

            _logger.LogInformation("Retrieved {Count} loan alias rows.", rows.Count);
            return rows;
        }

        public async Task<LoanAliasDto?> GetByIdAsync(long loanAliasId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(GetByIdSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var ordinals = GetOrdinals(reader);
            return MapRow(reader, ordinals);
        }

        public async Task<long> SaveAsync(LoanAliasSaveRequest request)
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
                command.Parameters.AddWithValue("@loan_alias_id", newId);
                command.Parameters.AddWithValue("@loan_alias_name", request.LoanAliasName);
                command.Parameters.AddWithValue("@created_by", request.CreatedBy);

                await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Created loan alias row with id {LoanAliasId}.", newId);
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

        public async Task<bool> UpdateAsync(long loanAliasId, LoanAliasUpdateRequest request)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(UpdateSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);
            command.Parameters.AddWithValue("@loan_alias_name", request.LoanAliasName);
            command.Parameters.AddWithValue("@updated_by", request.UpdatedBy);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Updated loan alias row {LoanAliasId}. Rows affected: {AffectedRows}",
                    loanAliasId,
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No row updated for loan_alias_id {LoanAliasId}.", loanAliasId);
            return false;
        }

        public async Task<bool> DeleteAsync(long loanAliasId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(DeleteSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            command.Parameters.AddWithValue("@loan_alias_id", loanAliasId);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Deleted loan alias row {LoanAliasId}. Rows affected: {AffectedRows}",
                    loanAliasId,
                    affectedRows);
                return true;
            }

            _logger.LogWarning("No row deleted for loan_alias_id {LoanAliasId}.", loanAliasId);
            return false;
        }

        private static (int Id, int Name, int CreatedBy, int CreatedDtm, int UpdatedBy, int UpdatedDtm) GetOrdinals(SqlDataReader reader)
        {
            return (
                reader.GetOrdinal("loan_alias_id"),
                reader.GetOrdinal("loan_alias_name"),
                reader.GetOrdinal("created_by"),
                reader.GetOrdinal("created_dtm"),
                reader.GetOrdinal("updated_by"),
                reader.GetOrdinal("updated_dtm"));
        }

        private static LoanAliasDto MapRow(
            SqlDataReader reader,
            (int Id, int Name, int CreatedBy, int CreatedDtm, int UpdatedBy, int UpdatedDtm) ordinals)
        {
            return new LoanAliasDto
            {
                LoanAliasId = reader.IsDBNull(ordinals.Id) ? 0L : Convert.ToInt64(reader.GetValue(ordinals.Id)),
                LoanAliasName = reader.IsDBNull(ordinals.Name) ? string.Empty : reader.GetString(ordinals.Name),
                CreatedBy = reader.IsDBNull(ordinals.CreatedBy) ? string.Empty : reader.GetString(ordinals.CreatedBy),
                CreatedDtm = reader.IsDBNull(ordinals.CreatedDtm) ? null : reader.GetDateTime(ordinals.CreatedDtm),
                UpdatedBy = reader.IsDBNull(ordinals.UpdatedBy) ? string.Empty : reader.GetString(ordinals.UpdatedBy),
                UpdatedDtm = reader.IsDBNull(ordinals.UpdatedDtm) ? null : reader.GetDateTime(ordinals.UpdatedDtm)
            };
        }
    }
}
