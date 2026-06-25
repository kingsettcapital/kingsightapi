using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{
    public interface ILoanService
    {
        Task<IReadOnlyList<LoanDto>> GetAllAsync();
        Task<bool> UpdateAsync(LoanUpdateBatchRequest request, string auditDisplayName);
    }

    public sealed class LoanService : ILoanService
    {
        private readonly string ListSql;
        private readonly string UpdateSql;

        private readonly string _connectionString;
        private readonly ILogger<LoanService> _logger;

        public LoanService(IConfiguration configuration, FabricWarehouseTables tables, ILogger<LoanService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;

            var dimLoan = tables.Mort("dim_loan");
            var loanAliasMaster = tables.Mort("loan_alias_master");
            var dimInvestor = tables.Mort("dim_investor");

            ListSql = $"""
                select l.loan_key,
                       l.loan_code,
                       l.loan_desc,
                       l.loan_alias_key,
                       loan_alias_name = isnull(m.loan_alias_name, ''),
                       investor_name = isnull(i.investor_name, ''),
                       l.loan_ranking,
                       dummy_loan_link = isnull(l.dummy_loan_link, ''),
                       l.is_loan_interest_applicable,
                       late_interest_off_note = isnull(l.late_interest_off_note, ''),
                       l.user_updated_by,
                       l.user_updated_date
                from {dimLoan} l
                left join {loanAliasMaster} m
                    on l.loan_alias_key = m.loan_alias_id
                left join {dimInvestor} i
                    on l.investor_key = i.investor_key
                   and i.is_current = 1
                where l.is_current = 1
                order by l.loan_code
                """;

            UpdateSql = $"""
                update {dimLoan}
                set loan_alias_key = @loan_alias_key,
                    loan_ranking = @loan_ranking,
                    dummy_loan_link = @dummy_loan_link,
                    is_loan_interest_applicable = @is_loan_interest_applicable,
                    late_interest_off_note = @late_interest_off_note,
                    user_updated_by = @user_updated_by,
                    user_updated_date = getutcdate()
                where loan_key = @loan_key
                  and is_current = 1
                """;
        }

        public async Task<IReadOnlyList<LoanDto>> GetAllAsync()
        {
            var rows = new List<LoanDto>();

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
                rows.Add(new LoanDto
                {
                    LoanKey = reader.IsDBNull(ordinals.Key)
                        ? 0L
                        : Convert.ToInt64(reader.GetValue(ordinals.Key)),
                    LoanCode = reader.IsDBNull(ordinals.Code)
                        ? string.Empty
                        : reader.GetString(ordinals.Code),
                    LoanDesc = reader.IsDBNull(ordinals.Desc)
                        ? string.Empty
                        : reader.GetString(ordinals.Desc),
                    LoanAliasKey = reader.IsDBNull(ordinals.AliasKey)
                        ? null
                        : Convert.ToInt32(reader.GetValue(ordinals.AliasKey)),
                    LoanAliasName = reader.IsDBNull(ordinals.AliasName)
                        ? string.Empty
                        : reader.GetString(ordinals.AliasName),
                    InvestorName = reader.IsDBNull(ordinals.InvestorName)
                        ? string.Empty
                        : reader.GetString(ordinals.InvestorName),
                    LoanRanking = reader.IsDBNull(ordinals.Ranking)
                        ? null
                        : reader.GetInt16(ordinals.Ranking),
                    DummyLoanLink = reader.IsDBNull(ordinals.DummyLink)
                        ? string.Empty
                        : reader.GetString(ordinals.DummyLink),
                    IsLoanInterestApplicable = reader.IsDBNull(ordinals.InterestApplicable)
                        ? null
                        : reader.GetBoolean(ordinals.InterestApplicable),
                    LateInterestOffNote = reader.IsDBNull(ordinals.LateNote)
                        ? string.Empty
                        : reader.GetString(ordinals.LateNote),
                    UserUpdatedBy = reader.IsDBNull(ordinals.UpdatedBy)
                        ? string.Empty
                        : reader.GetString(ordinals.UpdatedBy),
                    UserUpdatedDate = reader.IsDBNull(ordinals.UpdatedDate)
                        ? null
                        : reader.GetDateTime(ordinals.UpdatedDate)
                });
            }

            _logger.LogInformation("Retrieved {Count} current loan rows.", rows.Count);
            return rows;
        }

        public async Task<bool> UpdateAsync(LoanUpdateBatchRequest request, string auditDisplayName)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var affectedRows = 0;
            foreach (var loan in request.Loans)
            {
                await using var command = new SqlCommand(UpdateSql, connection)
                {
                    CommandType = System.Data.CommandType.Text
                };
                command.Parameters.AddWithValue("@loan_key", loan.LoanKey);
                command.Parameters.AddWithValue(
                    "@loan_alias_key",
                    loan.LoanAliasKey.HasValue ? loan.LoanAliasKey.Value : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@loan_ranking",
                    loan.LoanRanking.HasValue ? loan.LoanRanking.Value : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@dummy_loan_link",
                    string.IsNullOrEmpty(loan.DummyLoanLink) ? DBNull.Value : loan.DummyLoanLink);
                command.Parameters.AddWithValue(
                    "@is_loan_interest_applicable",
                    loan.IsLoanInterestApplicable.HasValue
                        ? loan.IsLoanInterestApplicable.Value
                        : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@late_interest_off_note",
                    string.IsNullOrEmpty(loan.LateInterestOffNote) ? DBNull.Value : loan.LateInterestOffNote);
                command.Parameters.AddWithValue("@user_updated_by", auditDisplayName);

                affectedRows += await command.ExecuteNonQueryAsync();
            }

            if (affectedRows > 0)
            {
                _logger.LogInformation("Loan rows affected: {AffectedRows}", affectedRows);
                return true;
            }

            _logger.LogWarning("No loan rows updated.");
            return false;
        }

        private static (
            int Key,
            int Code,
            int Desc,
            int AliasKey,
            int AliasName,
            int InvestorName,
            int Ranking,
            int DummyLink,
            int InterestApplicable,
            int LateNote,
            int UpdatedBy,
            int UpdatedDate) GetOrdinals(SqlDataReader reader)
        {
            return (
                reader.GetOrdinal("loan_key"),
                reader.GetOrdinal("loan_code"),
                reader.GetOrdinal("loan_desc"),
                reader.GetOrdinal("loan_alias_key"),
                reader.GetOrdinal("loan_alias_name"),
                reader.GetOrdinal("investor_name"),
                reader.GetOrdinal("loan_ranking"),
                reader.GetOrdinal("dummy_loan_link"),
                reader.GetOrdinal("is_loan_interest_applicable"),
                reader.GetOrdinal("late_interest_off_note"),
                reader.GetOrdinal("user_updated_by"),
                reader.GetOrdinal("user_updated_date"));
        }
    }
}
