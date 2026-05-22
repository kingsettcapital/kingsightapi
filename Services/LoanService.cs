using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace kingsightapi.Services
{
    public interface ILoanService
    {
        Task<PagedResult<LoanDto>> GetLoansAsync(
            string? description,
            string? status,
            string? loanAlias,
            int page,
            int pageSize);
        Task<bool> UpdateLoanAsync(int loanKey, LoanUpdateRequest request);
        Task<IReadOnlyList<InvestorDto>> GetInvestorDetailsAsync();
        Task<bool> UpdateInvestorAliasAsync(int loanKey, InvestorAliasUpdateRequest request);
        Task<IReadOnlyList<LoanAlias>> GetLoanAlias();
        Task<LoanBatchUpsertResult> LoanAliasUpdate(LoanAliasParent request, string? updatedBy = null);
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

        public async Task<PagedResult<LoanDto>> GetLoansAsync(
            string? description,
            string? status,
            string? loanAlias,
            int page,
            int pageSize)
        {
            var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
            var whereClause = BuildLoanListWhereClause(description, status, loanAlias);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var countSql = new StringBuilder();
            countSql.Append(" select count(*) ");
            countSql.Append(" from mort.dim_loan a ");
            countSql.Append(" inner join mort.dim_investor b on a.investor_key = b.investor_key ");
            countSql.Append(" where a.is_current = 1 ");
            countSql.Append(whereClause);

            await using var countCommand = new SqlCommand(countSql.ToString(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            AddLoanListParameters(countCommand, description, status, loanAlias);
            var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

            var dataSql = new StringBuilder();
            dataSql.Append(" select a.loan_key, ");
            dataSql.Append("        a.loan_code, ");
            dataSql.Append("        a.loan_desc, ");
            dataSql.Append("        a.loan_alias_name, ");
            dataSql.Append("        a.loan_ranking, ");
            // dataSql.Append("        a.created_date, ");
            dataSql.Append("        a.user_updated_date, ");
            dataSql.Append("        a.user_updated_by, ");
            dataSql.Append("        b.investor_name ");
            dataSql.Append(" from mort.dim_loan a ");
            dataSql.Append(" inner join mort.dim_investor b on a.investor_key = b.investor_key ");
            dataSql.Append(" where a.is_current = 1 ");
            dataSql.Append(whereClause);
            dataSql.Append(" order by a.loan_code ");
            dataSql.Append(" offset @offset rows fetch next @pageSize rows only ");

            await using var command = new SqlCommand(dataSql.ToString(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            AddLoanListParameters(command, description, status, loanAlias);
            command.Parameters.AddWithValue("@offset", offset);
            command.Parameters.AddWithValue("@pageSize", normalizedPageSize);

            var loans = new List<LoanDto>();

            await using var reader = await command.ExecuteReaderAsync();

            var loanKeyOrdinal = reader.GetOrdinal("loan_key");
            var loanCodeOrdinal = reader.GetOrdinal("loan_code");
            var loanDescOrdinal = reader.GetOrdinal("loan_desc");
            var loanAliasNameOrdinal = reader.GetOrdinal("loan_alias_name");
            var investorNameOrdinal = reader.GetOrdinal("investor_name");
            var loanRankingOrdinal = reader.GetOrdinal("loan_ranking");
            // var createdDateOrdinal = reader.GetOrdinal("created_date");
            var userUpdatedDateOrdinal = reader.GetOrdinal("user_updated_date");
            var userUpdatedByOrdinal = reader.GetOrdinal("user_updated_by");

            while (await reader.ReadAsync())
            {
                loans.Add(new LoanDto
                {
                    LoanKey = reader.IsDBNull(loanKeyOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(loanKeyOrdinal)),
                    LoanCode = reader.IsDBNull(loanCodeOrdinal) ? string.Empty : reader.GetString(loanCodeOrdinal),
                    LoanDesc = reader.IsDBNull(loanDescOrdinal) ? string.Empty : reader.GetString(loanDescOrdinal),
                    InvestorName = reader.IsDBNull(investorNameOrdinal) ? string.Empty : reader.GetString(investorNameOrdinal),
                    LoanAliasName = reader.IsDBNull(loanAliasNameOrdinal) ? string.Empty : reader.GetString(loanAliasNameOrdinal),
                    LoanRanking = reader.IsDBNull(loanRankingOrdinal) ? (short)0 : reader.GetInt16(loanRankingOrdinal),
                    // CreatedDate = reader.IsDBNull(createdDateOrdinal) ? null : reader.GetDateTime(createdDateOrdinal),
                    UserUpdatedDate = reader.IsDBNull(userUpdatedDateOrdinal) ? null : reader.GetDateTime(userUpdatedDateOrdinal),
                    UserUpdatedBy = reader.IsDBNull(userUpdatedByOrdinal) ? string.Empty : reader.GetString(userUpdatedByOrdinal)
                });
            }

            _logger.LogInformation(
                "Retrieved {Count} loans (page {Page}, pageSize {PageSize}, total {Total}).",
                loans.Count, normalizedPage, normalizedPageSize, totalCount);

            return new PagedResult<LoanDto>
            {
                Items = loans,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount
            };
        }

        private static string BuildLoanListWhereClause(
            string? description,
            string? status,
            string? loanAlias)
        {
            var filter = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(description))
            {
                filter.Append(" and (a.loan_desc like @description or a.loan_alias_name like @description) ");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                filter.Append(" and a.level_1_desc = @status ");
            }

            if (!string.IsNullOrWhiteSpace(loanAlias))
            {
                filter.Append(" and a.loan_alias_name = @loanAlias ");
            }

            return filter.ToString();
        }

        private static void AddLoanListParameters(
            SqlCommand command,
            string? description,
            string? status,
            string? loanAlias)
        {
            if (!string.IsNullOrWhiteSpace(description))
            {
                command.Parameters.AddWithValue("@description", $"%{description.Trim()}%");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                command.Parameters.AddWithValue("@status", status.Trim());
            }

            if (!string.IsNullOrWhiteSpace(loanAlias))
            {
                command.Parameters.AddWithValue("@loanAlias", loanAlias.Trim());
            }
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
            var userUpdatedDateOrdinal = reader.GetOrdinal("user_updated_date");
            var userUpdatedByOrdinal = reader.GetOrdinal("user_updated_by");

            while (await reader.ReadAsync())
            {
                rows.Add(new LoanAlias
                {
                    LoanKey = reader.IsDBNull(loanKeyOrdinal) ? 0 : Convert.ToInt64(reader.GetValue(loanKeyOrdinal)),
                    LoanAliasName = reader.IsDBNull(loanAliasNameOrdinal) ? string.Empty : reader.GetString(loanAliasNameOrdinal),
                    LoanRanking = reader.IsDBNull(loanRankingOrdinal) ? null : reader.GetInt16(loanRankingOrdinal),
                    UserUpdatedDate = reader.IsDBNull(userUpdatedDateOrdinal) ? null : reader.GetDateTime(userUpdatedDateOrdinal),
                    UserUpdatedBy = reader.IsDBNull(userUpdatedByOrdinal) ? string.Empty : reader.GetString(userUpdatedByOrdinal)
                });
            }

            _logger.LogInformation("Retrieved {Count} loan alias records from Fabric.", rows.Count);
            return rows;
        }

        public async Task<LoanBatchUpsertResult> LoanAliasUpdate(LoanAliasParent request, string? updatedBy = null)
        {
            if (request?.LoanAliases is null || request.LoanAliases.Count == 0)
            {
                return new LoanBatchUpsertResult();
            }

            var failed = new List<long>();
            var updated = 0;
            var inserted = 0;
            var auditUser = updatedBy ?? "System";
            var auditDate = DateTime.UtcNow;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var alias in request.LoanAliases)
            {
                var aliasName = alias.LoanAliasName ?? string.Empty;
                var ranking = alias.LoanRanking ?? (short)0;
                var user = alias.UserUpdatedBy ?? auditUser;
                var affected = 0;

                if (alias.LoanKey > 0)
                {
                    affected = await ExecuteLoanSyndicateUpdateByKeyAsync(
                        connection, alias.LoanKey, aliasName, ranking, auditDate, user);
                }

                if (affected == 0 && !string.IsNullOrWhiteSpace(alias.LoanCode))
                {
                    affected = await ExecuteLoanSyndicateUpdateByCodeAsync(
                        connection, alias.LoanCode, aliasName, ranking, auditDate, user);
                }

                if (affected > 0)
                {
                    updated++;
                    continue;
                }

                string? error = null;
                if (TryInsertLoan(alias, out error))
                {
                    var insertAffected = await ExecuteLoanSyndicateInsertAsync(
                        connection, alias.LoanCode, aliasName, ranking, auditDate, user);

                    if (insertAffected > 0)
                    {
                        inserted++;
                        continue;
                    }

                    error ??= "Insert failed.";
                }
                else
                {
                    error ??= "Not found.";
                }

                failed.Add(alias.LoanKey > 0 ? alias.LoanKey : 0);
                _logger.LogWarning(
                    "Loan alias upsert failed for key {LoanKey}, code {LoanCode}: {Reason}",
                    alias.LoanKey, alias.LoanCode, error);
            }

            _logger.LogInformation(
                "Loan alias upsert: updated {Updated}, inserted {Inserted}, failed {Failed}.",
                updated, inserted, failed.Count);

            return new LoanBatchUpsertResult
            {
                UpdatedCount = updated,
                InsertedCount = inserted,
                FailedLoanKeys = failed
            };
        }

        private const string UpdateLoanSyndicateByKeySql = """
            update mort.dim_loan
            set loan_alias_name = @loan_alias_name,
                loan_ranking = @loan_ranking,
                user_updated_date = @user_updated_date,
                user_updated_by = @user_updated_by
            where loan_key = @loan_key
              and is_current = 1
            """;

        private const string UpdateLoanSyndicateByCodeSql = """
            update mort.dim_loan
            set loan_alias_name = @loan_alias_name,
                loan_ranking = @loan_ranking,
                user_updated_date = @user_updated_date,
                user_updated_by = @user_updated_by
            where loan_code = @loan_code
              and is_current = 1
            """;

        private const string InsertLoanSyndicateSql = """
            insert into mort.dim_loan (
                loan_code,
                loan_alias_name,
                loan_ranking,
                is_current,
                user_updated_date,
                user_updated_by)
            values (
                @loan_code,
                @loan_alias_name,
                @loan_ranking,
                1,
                @user_updated_date,
                @user_updated_by)
            """;

        private static bool TryInsertLoan(LoanAlias alias, out string? error)
        {
            if (string.IsNullOrWhiteSpace(alias.LoanCode))
            {
                error = "loanCode is required to insert a new loan.";
                return false;
            }

            error = null;
            return true;
        }

        private static async Task<int> ExecuteLoanSyndicateUpdateByKeyAsync(
            SqlConnection connection,
            long loanKey,
            string alias,
            short ranking,
            DateTime auditDate,
            string user)
        {
            await using var command = new SqlCommand(UpdateLoanSyndicateByKeySql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            command.Parameters.AddWithValue("@loan_key", loanKey);
            command.Parameters.AddWithValue("@loan_alias_name", alias);
            command.Parameters.AddWithValue("@loan_ranking", ranking);
            command.Parameters.AddWithValue("@user_updated_date", auditDate);
            command.Parameters.AddWithValue("@user_updated_by", user);

            return await command.ExecuteNonQueryAsync();
        }

        private static async Task<int> ExecuteLoanSyndicateUpdateByCodeAsync(
            SqlConnection connection,
            string loanCode,
            string alias,
            short ranking,
            DateTime auditDate,
            string user)
        {
            await using var command = new SqlCommand(UpdateLoanSyndicateByCodeSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            command.Parameters.AddWithValue("@loan_code", loanCode.Trim());
            command.Parameters.AddWithValue("@loan_alias_name", alias);
            command.Parameters.AddWithValue("@loan_ranking", ranking);
            command.Parameters.AddWithValue("@user_updated_date", auditDate);
            command.Parameters.AddWithValue("@user_updated_by", user);

            return await command.ExecuteNonQueryAsync();
        }

        private static async Task<int> ExecuteLoanSyndicateInsertAsync(
            SqlConnection connection,
            string loanCode,
            string alias,
            short ranking,
            DateTime auditDate,
            string user)
        {
            await using var command = new SqlCommand(InsertLoanSyndicateSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            command.Parameters.AddWithValue("@loan_code", loanCode.Trim());
            command.Parameters.AddWithValue("@loan_alias_name", alias);
            command.Parameters.AddWithValue("@loan_ranking", ranking);
            command.Parameters.AddWithValue("@user_updated_date", auditDate);
            command.Parameters.AddWithValue("@user_updated_by", user);

            return await command.ExecuteNonQueryAsync();
        }
    }
}