using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace kingsightapi.Services
{
    public interface IInvestorService
    {
        Task<PagedResult<DimInvestorDto>> GetInvestorsAsync(
            IReadOnlyList<string>? investorNames,
            int page,
            int pageSize);
        Task<DimInvestorAliasBatchUpdateResult> UpdateInvestorAliasesAsync(
            DimInvestorAliasBatchUpdateRequest request,
            string? updatedBy = null);
        Task<IReadOnlyList<InvestorNameOptionDto>> GetInvestorNameOptionsAsync();
    }

    public sealed class InvestorService : IInvestorService
    {
        private const string UpdateInvestorAliasByKeySql = """
            update mort.dim_investor
            set investor_alias_name = @investor_alias_name,
                user_updated_date = @user_updated_date,
                user_updated_by = @user_updated_by
            where investor_key = @investor_key
              and is_current = 1
            """;

        private const string UpdateInvestorAliasByCodeSql = """
            update mort.dim_investor
            set investor_alias_name = @investor_alias_name,
                user_updated_date = @user_updated_date,
                user_updated_by = @user_updated_by
            where investor_code = @investor_code
              and is_current = 1
            """;

        private const string InsertInvestorSql = """
            insert into mort.dim_investor (
                investor_code,
                investor_name,
                investor_alias_name,
                is_current,
                user_updated_date,
                user_updated_by)
            values (
                @investor_code,
                @investor_name,
                @investor_alias_name,
                1,
                @user_updated_date,
                @user_updated_by)
            """;

        private readonly string _connectionString;
        private readonly ILogger<InvestorService> _logger;

        public InvestorService(IConfiguration configuration, ILogger<InvestorService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
        }

        public async Task<PagedResult<DimInvestorDto>> GetInvestorsAsync(
            IReadOnlyList<string>? investorNames,
            int page,
            int pageSize)
        {
            var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

            var names = investorNames?
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            var nameFilter = BuildInvestorNameFilter(names);

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var countSql = new StringBuilder();
            countSql.Append(" select count(*) ");
            countSql.Append(" from mort.dim_investor ");
            countSql.Append(" where is_current = 1 ");
            countSql.Append(nameFilter);

            await using var countCommand = new SqlCommand(countSql.ToString(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            AddInvestorNameParameters(countCommand, names);
            var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

            var dataSql = new StringBuilder();
            dataSql.Append(" select investor_key, ");
            dataSql.Append("        investor_code, ");
            dataSql.Append("        investor_name, ");
            dataSql.Append("        investor_alias_name, ");
            dataSql.Append("        user_updated_date, ");
            dataSql.Append("        user_updated_by ");
            dataSql.Append(" from mort.dim_investor ");
            dataSql.Append(" where is_current = 1 ");
            dataSql.Append(nameFilter);
            dataSql.Append(" order by investor_name ");
            dataSql.Append(" offset @offset rows fetch next @pageSize rows only ");

            await using var command = new SqlCommand(dataSql.ToString(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };
            AddInvestorNameParameters(command, names);
            command.Parameters.AddWithValue("@offset", offset);
            command.Parameters.AddWithValue("@pageSize", normalizedPageSize);

            var rows = new List<DimInvestorDto>();

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
                    user_updated_date = reader.IsDBNull(userUpdatedDateOrdinal) ? null : reader.GetDateTime(userUpdatedDateOrdinal),
                    user_updated_by = reader.IsDBNull(userUpdatedByOrdinal) ? string.Empty : reader.GetString(userUpdatedByOrdinal)
                });
            }

            _logger.LogInformation(
                "Retrieved {Count} investors (page {Page}, pageSize {PageSize}, total {Total}).",
                rows.Count, normalizedPage, normalizedPageSize, totalCount);

            return new PagedResult<DimInvestorDto>
            {
                Items = rows,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount
            };
        }

        public async Task<DimInvestorAliasBatchUpdateResult> UpdateInvestorAliasesAsync(
            DimInvestorAliasBatchUpdateRequest request,
            string? updatedBy = null)
        {
            if (request.Investors is null || request.Investors.Count == 0)
            {
                return new DimInvestorAliasBatchUpdateResult();
            }

            var failed = new List<long>();
            var updated = 0;
            var inserted = 0;
            var auditUser = updatedBy ?? "System";
            var auditDate = DateTime.UtcNow;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var item in request.Investors)
            {
                var alias = item.investor_alias_name ?? string.Empty;
                var user = item.user_updated_by ?? auditUser;
                var affected = 0;

                if (item.investor_key > 0)
                {
                    affected = await ExecuteInvestorUpdateByKeyAsync(
                        connection, item.investor_key, alias, auditDate, user);
                }

                if (affected == 0 && !string.IsNullOrWhiteSpace(item.investor_code))
                {
                    affected = await ExecuteInvestorUpdateByCodeAsync(
                        connection, item.investor_code, alias, auditDate, user);
                }

                if (affected > 0)
                {
                    updated++;
                    continue;
                }

                string? error = null;
                if (TryInsertInvestor(item, out error))
                {
                    var insertAffected = await ExecuteInvestorInsertAsync(
                        connection, item.investor_code, item.investor_name, alias, auditDate, user);

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

                failed.Add(item.investor_key > 0 ? item.investor_key : 0);
                _logger.LogWarning(
                    "Investor alias upsert failed for key {InvestorKey}, code {InvestorCode}: {Reason}",
                    item.investor_key, item.investor_code, error);
            }

            _logger.LogInformation(
                "Investor alias upsert: updated {Updated}, inserted {Inserted}, failed {Failed}.",
                updated, inserted, failed.Count);

            return new DimInvestorAliasBatchUpdateResult
            {
                UpdatedCount = updated,
                InsertedCount = inserted,
                FailedInvestorKeys = failed
            };
        }

        public async Task<IReadOnlyList<InvestorNameOptionDto>> GetInvestorNameOptionsAsync()
        {
            var sql = new StringBuilder();
            sql.Append(" select investor_key, investor_code, investor_name ");
            sql.Append(" from mort.dim_investor ");
            sql.Append(" where is_current = 1 ");
            sql.Append(" order by investor_name ");

            var rows = new List<InvestorNameOptionDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql.ToString(), connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            await using var reader = await command.ExecuteReaderAsync();

            var investorKeyOrdinal = reader.GetOrdinal("investor_key");
            var investorCodeOrdinal = reader.GetOrdinal("investor_code");
            var investorNameOrdinal = reader.GetOrdinal("investor_name");

            while (await reader.ReadAsync())
            {
                rows.Add(new InvestorNameOptionDto
                {
                    investor_key = reader.IsDBNull(investorKeyOrdinal) ? 0L : Convert.ToInt64(reader.GetValue(investorKeyOrdinal)),
                    investor_code = reader.IsDBNull(investorCodeOrdinal) ? string.Empty : reader.GetString(investorCodeOrdinal),
                    investor_name = reader.IsDBNull(investorNameOrdinal) ? string.Empty : reader.GetString(investorNameOrdinal)
                });
            }

            _logger.LogInformation("Retrieved {Count} investor name options.", rows.Count);
            return rows;
        }

        private static string BuildInvestorNameFilter(IReadOnlyList<string> names)
        {
            if (names.Count == 0)
            {
                return string.Empty;
            }

            var filter = new StringBuilder(" and investor_name in (");
            for (var i = 0; i < names.Count; i++)
            {
                if (i > 0)
                {
                    filter.Append(',');
                }

                filter.Append($"@name{i}");
            }

            filter.Append(')');
            return filter.ToString();
        }

        private static void AddInvestorNameParameters(SqlCommand command, IReadOnlyList<string> names)
        {
            for (var i = 0; i < names.Count; i++)
            {
                command.Parameters.AddWithValue($"@name{i}", names[i]);
            }
        }

        private static bool TryInsertInvestor(DimInvestorAliasUpdateItem item, out string? error)
        {
            if (string.IsNullOrWhiteSpace(item.investor_code))
            {
                error = "investor_code is required to insert a new investor.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.investor_name))
            {
                error = "investor_name is required to insert a new investor.";
                return false;
            }

            error = null;
            return true;
        }

        private static async Task<int> ExecuteInvestorUpdateByKeyAsync(
            SqlConnection connection,
            long investorKey,
            string alias,
            DateTime auditDate,
            string user)
        {
            await using var command = new SqlCommand(UpdateInvestorAliasByKeySql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            command.Parameters.AddWithValue("@investor_key", investorKey);
            command.Parameters.AddWithValue("@investor_alias_name", alias);
            command.Parameters.AddWithValue("@user_updated_date", auditDate);
            command.Parameters.AddWithValue("@user_updated_by", user);

            return await command.ExecuteNonQueryAsync();
        }

        private static async Task<int> ExecuteInvestorUpdateByCodeAsync(
            SqlConnection connection,
            string investorCode,
            string alias,
            DateTime auditDate,
            string user)
        {
            await using var command = new SqlCommand(UpdateInvestorAliasByCodeSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            command.Parameters.AddWithValue("@investor_code", investorCode.Trim());
            command.Parameters.AddWithValue("@investor_alias_name", alias);
            command.Parameters.AddWithValue("@user_updated_date", auditDate);
            command.Parameters.AddWithValue("@user_updated_by", user);

            return await command.ExecuteNonQueryAsync();
        }

        private static async Task<int> ExecuteInvestorInsertAsync(
            SqlConnection connection,
            string investorCode,
            string investorName,
            string alias,
            DateTime auditDate,
            string user)
        {
            await using var command = new SqlCommand(InsertInvestorSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            command.Parameters.AddWithValue("@investor_code", investorCode.Trim());
            command.Parameters.AddWithValue("@investor_name", investorName.Trim());
            command.Parameters.AddWithValue("@investor_alias_name", alias);
            command.Parameters.AddWithValue("@user_updated_date", auditDate);
            command.Parameters.AddWithValue("@user_updated_by", user);

            return await command.ExecuteNonQueryAsync();
        }
    }
}
