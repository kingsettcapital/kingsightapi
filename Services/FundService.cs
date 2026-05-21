using kingsightapi.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace kingsightapi.Services
{

    public interface IFundService
    {
        Task<IReadOnlyList<FundDto>> GetFundsAsync();
    }

    public sealed class FundService : IFundService
    {
        private const string FundSql = """
                                   select fund_key,fund_id, fund_code,fund_name,fund_type_name,fund_strategy_name,
                                          js_fund_name,is_sidecar,fund_start_date
                                   from wh_enterprise_gold.dbo.dim_fund
                                   where getdate() between valid_from and isnull(valid_to,getdate())
                                   order by 1
                                   """;

        private readonly string _connectionString;
        private readonly ILogger<FundService> _logger;

        public FundService(IConfiguration configuration, ILogger<FundService> logger)
        {
            _connectionString = configuration.GetConnectionString("FabricConnectionString")
                ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
            _logger = logger;
        }

        public async Task<IReadOnlyList<FundDto>> GetFundsAsync()
        {
            var funds = new List<FundDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(FundSql, connection)
            {
                CommandType = System.Data.CommandType.Text
            };

            await using var reader = await command.ExecuteReaderAsync();

            var fundKeyOrdinal = reader.GetOrdinal("fund_key");
            var fundIdOrdinal = reader.GetOrdinal("fund_id");
            var fundCodeOrdinal = reader.GetOrdinal("fund_code");
            var fundNameOrdinal = reader.GetOrdinal("fund_name");
            var fundTypeNameOrdinal = reader.GetOrdinal("fund_type_name");
            var fundStrategyNameOrdinal = reader.GetOrdinal("fund_strategy_name");
            var jsFundNameOrdinal = reader.GetOrdinal("js_fund_name");
            var isSidecarOrdinal = reader.GetOrdinal("is_sidecar");
            var fundStartDateOrdinal = reader.GetOrdinal("fund_start_date");

            while (await reader.ReadAsync())
            {
                var fund = new FundDto
                {
                    FundKey = reader.IsDBNull(fundKeyOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(fundKeyOrdinal)),
                    FundId = reader.IsDBNull(fundIdOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(fundIdOrdinal)),
                    FundCode = reader.IsDBNull(fundCodeOrdinal) ? string.Empty : reader.GetString(fundCodeOrdinal),
                    FundName = reader.IsDBNull(fundNameOrdinal) ? string.Empty : reader.GetString(fundNameOrdinal),
                    FundTypeName = reader.IsDBNull(fundTypeNameOrdinal) ? string.Empty : reader.GetString(fundTypeNameOrdinal),
                    FundStrategyName = reader.IsDBNull(fundStrategyNameOrdinal) ? string.Empty : reader.GetString(fundStrategyNameOrdinal),
                    JsFundName = reader.IsDBNull(jsFundNameOrdinal) ? string.Empty : reader.GetString(jsFundNameOrdinal),
                    IsSidecar = !reader.IsDBNull(isSidecarOrdinal) && Convert.ToBoolean(reader.GetValue(isSidecarOrdinal)),
                    FundStartDate = reader.IsDBNull(fundStartDateOrdinal) ? null : reader.GetDateTime(fundStartDateOrdinal)
                };

                funds.Add(fund);
            }

            _logger.LogInformation("Retrieved {Count} fund records from Fabric.", funds.Count);
            return funds;
        }
    }
}