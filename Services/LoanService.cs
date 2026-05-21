using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace kingsightapi.Services
{
    public interface ILoanService
    {
        Task<IReadOnlyList<LoanDto>> GetLoansAsync();
        Task<bool> UpdateLoanAsync(int loanKey, LoanUpdateRequest request);
        Task<IReadOnlyList<InvestorDto>> GetInvestorDetailsAsync();
        Task<bool> UpdateInvestorAliasAsync(int loanKey, InvestorAliasUpdateRequest request);
        Task<IReadOnlyList<LoanAlias>> GetLoanAlias();
        Task<int> LoanAliasUpdate(LoanAliasParent request);
    }

    public sealed class LoanService : ILoanService
    {
       


        private const string UpdateInvestorAliasSql = """
                                                update mort.dim_loan
                                                set investor_alias_name = @investor_alias_name,
                                                    user_updated_date = @user_updated_date,
                                                    user_updated_by = @user_updated_by
                                                where loan_key = @loan_key
                                                """;

        private const string InvestorDetailsSql = """
            select loan_key,
                   investor_name,
                   investor_alias_name,
                   user_updated_date,
                   user_updated_by
            from mort.dim_loan
            order by loan_key
            """;

        private readonly string _connectionString;
        private readonly ILogger<LoanService> _logger;

        public LoanService(IConfiguration configuration, ILogger<LoanService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
               ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
        }

        public async Task<IReadOnlyList<LoanDto>> GetLoansAsync()
        {
            StringBuilder sql = new StringBuilder();
            var loans = new List<LoanDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            sql.Append(" Select a.loan_key,a.loan_code,a.loan_desc,a.loan_alias_name,a.loan_ranking,a.loan_level, ");
            sql.Append(" a.is_leaf,a.level_1_code,a.level_1_desc,a.level_2_code,a.level_2_desc,a.level_3_code,a.level_3_desc, ");
            sql.Append(" a.created_date,a.last_refreshed_date,a.user_updated_date,a.user_updated_by,b.investor_name ");
            sql.Append(" from mort.dim_loan a inner join mort.dim_investor b ");
            sql.Append(" on a.investor_key = b.investor_key ");
            sql.Append(" where a.is_current=1 ");
            sql.Append(" order by loan_key ");

            await using var command = new SqlCommand(sql.ToString(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            await using var reader = await command.ExecuteReaderAsync();

            var loanKeyOrdinal = reader.GetOrdinal("loan_key");
            var loanCodeOrdinal = reader.GetOrdinal("loan_code");
            var loanDescOrdinal = reader.GetOrdinal("loan_desc");
            var loanAliasNameOrdinal = reader.GetOrdinal("loan_alias_name");
            var investorName = reader.GetOrdinal("investor_name");
            var loanRankingOrdinal = reader.GetOrdinal("loan_ranking");
            var loanLevelOrdinal = reader.GetOrdinal("loan_level");
            var isLeafOrdinal = reader.GetOrdinal("is_leaf");
            var level1CodeOrdinal = reader.GetOrdinal("level_1_code");
            var level1DescOrdinal = reader.GetOrdinal("level_1_desc");
            var level2CodeOrdinal = reader.GetOrdinal("level_2_code");
            var level2DescOrdinal = reader.GetOrdinal("level_2_desc");
            var level3CodeOrdinal = reader.GetOrdinal("level_3_code");
            var level3DescOrdinal = reader.GetOrdinal("level_3_desc");
            var createdDateOrdinal = reader.GetOrdinal("created_date");
            var lastRefreshedDateOrdinal = reader.GetOrdinal("last_refreshed_date");
            var userUpdatedDateOrdinal = reader.GetOrdinal("user_updated_date");
            var userUpdatedByOrdinal = reader.GetOrdinal("user_updated_by");

            while (await reader.ReadAsync())
            {
                var loan = new LoanDto
                {
                    LoanKey = reader.IsDBNull(loanKeyOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(loanKeyOrdinal)),
                    LoanCode = reader.IsDBNull(loanCodeOrdinal) ? string.Empty : reader.GetString(loanCodeOrdinal),
                    LoanDesc = reader.IsDBNull(loanDescOrdinal) ? string.Empty : reader.GetString(loanDescOrdinal),
                    InvestorName = reader.IsDBNull(investorName) ? string.Empty : reader.GetString(investorName),
                    LoanAliasName = reader.IsDBNull(loanAliasNameOrdinal) ? string.Empty : reader.GetString(loanAliasNameOrdinal),
                    LoanRanking = reader.IsDBNull(loanRankingOrdinal) ? (short)0 : reader.GetInt16(loanRankingOrdinal),
                    UserUpdatedDate = reader.IsDBNull(userUpdatedDateOrdinal) ? null : reader.GetDateTime(userUpdatedDateOrdinal),
                    UserUpdatedBy = reader.IsDBNull(userUpdatedByOrdinal) ? string.Empty : reader.GetString(userUpdatedByOrdinal)
                    //LoanLevel = reader.IsDBNull(loanLevelOrdinal) ? (short)0 : reader.GetInt16(loanLevelOrdinal),
                    //IsLeaf = !reader.IsDBNull(isLeafOrdinal) && Convert.ToBoolean(reader.GetValue(isLeafOrdinal)),
                    //Level1Code = reader.IsDBNull(level1CodeOrdinal) ? string.Empty : reader.GetString(level1CodeOrdinal),
                    //Level1Desc = reader.IsDBNull(level1DescOrdinal) ? string.Empty : reader.GetString(level1DescOrdinal),
                    //Level2Code = reader.IsDBNull(level2CodeOrdinal) ? string.Empty : reader.GetString(level2CodeOrdinal),
                    //Level2Desc = reader.IsDBNull(level2DescOrdinal) ? string.Empty : reader.GetString(level2DescOrdinal),
                    //Level3Code = reader.IsDBNull(level3CodeOrdinal) ? string.Empty : reader.GetString(level3CodeOrdinal),
                    //Level3Desc = reader.IsDBNull(level3DescOrdinal) ? string.Empty : reader.GetString(level3DescOrdinal),
                    //SponsorName = reader.IsDBNull(sponsorNameOrdinal) ? string.Empty : reader.GetString(sponsorNameOrdinal),
                    //CreatedDate = reader.IsDBNull(createdDateOrdinal) ? null : reader.GetDateTime(createdDateOrdinal),
                    //LastRefreshedDate = reader.IsDBNull(lastRefreshedDateOrdinal) ? null : reader.GetDateTime(lastRefreshedDateOrdinal),
                };

                loans.Add(loan);
            }

            _logger.LogInformation("Retrieved {Count} loan records from Fabric.", loans.Count);
            return loans;
        }

        public async Task<bool> UpdateLoanAsync(int loanKey, LoanUpdateRequest request)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append(" update mort.dim_loan ");
            sql.Append(" set loan_alias_name = @loan_alias_name, ");
            sql.Append("     loan_ranking = @loan_ranking, ");
            sql.Append("     user_updated_date = @user_updated_date, ");
            sql.Append("     user_updated_by = @user_updated_by ");
            sql.Append(" where loan_key = @loan_key ");

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql.ToString(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            command.Parameters.AddWithValue("@loan_key", loanKey);
            command.Parameters.AddWithValue("@loan_alias_name", request.LoanAliasName);
            command.Parameters.AddWithValue("@loan_ranking", request.LoanRanking!.Value);
            command.Parameters.AddWithValue("@user_updated_date", request.UserUpdatedDate ?? DateTime.UtcNow);
            command.Parameters.AddWithValue("@user_updated_by", request.UserUpdatedBy);

            var affectedRows = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Updated loan {LoanKey}. Rows affected: {AffectedRows}", loanKey, affectedRows);
            return affectedRows > 0;
        }

        public async Task<IReadOnlyList<InvestorDto>> GetInvestorDetailsAsync()
        {
            var rows = new List<InvestorDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(InvestorDetailsSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            await using var reader = await command.ExecuteReaderAsync();

            var loanKeyOrdinal = reader.GetOrdinal("loan_key");
            var investorNameOrdinal = reader.GetOrdinal("investor_name");
            var investorAliasNameOrdinal = reader.GetOrdinal("investor_alias_name");
            var userUpdatedDateOrdinal = reader.GetOrdinal("user_updated_date");
            var userUpdatedByOrdinal = reader.GetOrdinal("user_updated_by");

            while (await reader.ReadAsync())
            {
                rows.Add(new InvestorDto
                {
                    LoanKey = reader.IsDBNull(loanKeyOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(loanKeyOrdinal)),
                    InvestorName = reader.IsDBNull(investorNameOrdinal) ? string.Empty : reader.GetString(investorNameOrdinal),
                    InvestorAliasName = reader.IsDBNull(investorAliasNameOrdinal) ? string.Empty : reader.GetString(investorAliasNameOrdinal),
                    UserUpdatedDate = reader.IsDBNull(userUpdatedDateOrdinal) ? null : reader.GetDateTime(userUpdatedDateOrdinal),
                    UserUpdatedBy = reader.IsDBNull(userUpdatedByOrdinal) ? string.Empty : reader.GetString(userUpdatedByOrdinal)
                });
            }

            _logger.LogInformation("Retrieved {Count} investor detail records from Fabric.", rows.Count);
            return rows;
        }

        public async Task<bool> UpdateInvestorAliasAsync(int loanKey, InvestorAliasUpdateRequest request)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(UpdateInvestorAliasSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            command.Parameters.AddWithValue("@loan_key", loanKey);
            command.Parameters.AddWithValue("@investor_alias_name", request.InvestorAliasName);
            command.Parameters.AddWithValue("@user_updated_date", request.UserUpdatedDate ?? DateTime.UtcNow);
            command.Parameters.AddWithValue("@user_updated_by", request.UserUpdatedBy);

            var affectedRows = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Updated investor alias for loan {LoanKey}. Rows affected: {AffectedRows}", loanKey, affectedRows);
            return affectedRows > 0;
        }

        public async Task<IReadOnlyList<LoanAlias>> GetLoanAlias()
        {
            StringBuilder sql = new StringBuilder();
            var rows = new List<LoanAlias>();

            sql.Append(" select loan_key, ");
            sql.Append("        loan_alias_name, ");
            sql.Append("        loan_ranking, ");
            sql.Append("        is_loan_interest_applicable, ");
            sql.Append("        is_dummy_loan_identifier, ");
            sql.Append("        late_interest_off_notes, ");
            sql.Append("        user_updated_date, ");
            sql.Append("        user_updated_by ");
            sql.Append(" from mort.dim_loan ");
            sql.Append(" where is_current = 1 ");
            sql.Append(" order by loan_key ");

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql.ToString(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            await using var reader = await command.ExecuteReaderAsync();

            var loanKeyOrdinal = reader.GetOrdinal("loan_key");
            var loanAliasNameOrdinal = reader.GetOrdinal("loan_alias_name");
            var loanRankingOrdinal = reader.GetOrdinal("loan_ranking");
            var isLoanInterestApplicableOrdinal = reader.GetOrdinal("is_loan_interest_applicable");
            var isDummyLoanIdentifierOrdinal = reader.GetOrdinal("is_dummy_loan_identifier");
            var lateInterestOffNotesOrdinal = reader.GetOrdinal("late_interest_off_notes");
            var userUpdatedDateOrdinal = reader.GetOrdinal("user_updated_date");
            var userUpdatedByOrdinal = reader.GetOrdinal("user_updated_by");

            while (await reader.ReadAsync())
            {
                rows.Add(new LoanAlias
                {
                    loan_key = reader.IsDBNull(loanKeyOrdinal) ? 0 : Convert.ToInt64(reader.GetValue(loanKeyOrdinal)),
                    loan_alias_name = reader.IsDBNull(loanAliasNameOrdinal) ? string.Empty : reader.GetString(loanAliasNameOrdinal),
                    loan_ranking = reader.IsDBNull(loanRankingOrdinal) ? null : reader.GetInt16(loanRankingOrdinal),
                    is_loan_interest_applicable = reader.IsDBNull(isLoanInterestApplicableOrdinal) ? null : Convert.ToBoolean(reader.GetValue(isLoanInterestApplicableOrdinal)),
                    is_dummy_loan_identifier = reader.IsDBNull(isDummyLoanIdentifierOrdinal) ? null : Convert.ToBoolean(reader.GetValue(isDummyLoanIdentifierOrdinal)),
                    late_interest_off_notes = reader.IsDBNull(lateInterestOffNotesOrdinal) ? string.Empty : reader.GetString(lateInterestOffNotesOrdinal),
                    user_updated_date = reader.IsDBNull(userUpdatedDateOrdinal) ? null : reader.GetDateTime(userUpdatedDateOrdinal),
                    user_updated_by = reader.IsDBNull(userUpdatedByOrdinal) ? string.Empty : reader.GetString(userUpdatedByOrdinal)
                });
            }

            _logger.LogInformation("Retrieved {Count} loan alias records from Fabric.", rows.Count);
            return rows;
        }

        public async Task<int> LoanAliasUpdate(LoanAliasParent request)
        {
            if (request?.LoanAliases is null || request.LoanAliases.Count == 0)
            {
                return 0;
            }

            StringBuilder sql = new();
            sql.Append(" update mort.dim_loan ");
            sql.Append(" set loan_alias_name = @loan_alias_name, ");
            sql.Append("     loan_ranking = @loan_ranking, ");
            sql.Append("     is_loan_interest_applicable = @is_loan_interest_applicable, ");
            sql.Append("     is_dummy_loan_identifier = @is_dummy_loan_identifier, ");
            sql.Append("     late_interest_off_notes = @late_interest_off_notes, ");
            sql.Append("     user_updated_date = @user_updated_date, ");
            sql.Append("     user_updated_by = @user_updated_by ");
            sql.Append(" where loan_key = @loan_key ");

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var totalAffected = 0;

            foreach (var alias in request.LoanAliases)
            {
                await using var command = new SqlCommand(sql.ToString(), connection)
                {
                    CommandType = System.Data.CommandType.Text
                };

                command.Parameters.AddWithValue("@loan_key", alias.LoanKey);
                command.Parameters.AddWithValue("@loan_alias_name", (object?)alias.LoanAliasName ?? DBNull.Value);
                command.Parameters.AddWithValue("@loan_ranking", (object?)alias.LoanRanking ?? DBNull.Value);
                command.Parameters.AddWithValue("@is_loan_interest_applicable", (object?)alias.IsLoanInterestApplicable ?? DBNull.Value);
                command.Parameters.AddWithValue("@is_dummy_loan_identifier", (object?)alias.IsDummyLoanIdentifier ?? DBNull.Value);
                command.Parameters.AddWithValue("@late_interest_off_notes", (object?)alias.LateInterestOffNotes ?? DBNull.Value);
                command.Parameters.AddWithValue("@user_updated_date", alias.UserUpdatedDate ?? DateTime.UtcNow);
                command.Parameters.AddWithValue("@user_updated_by", (object?)alias.UserUpdatedBy ?? DBNull.Value);

                totalAffected += await command.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Updated {Count} loan alias records. Rows affected: {AffectedRows}", request.LoanAliases.Count, totalAffected);
            return totalAffected;
        }
    }
}