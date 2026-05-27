using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public interface IFundPortalService
{
    Task<PagedResult<FundListItemDto>> GetFundsAsync(string? search, int page, int pageSize);
    Task<FundDetailDto?> GetFundByKeyAsync(int fundKey);
    Task<IReadOnlyList<FundInvestorDto>> GetFundInvestorsAsync(int fundKey);
}

public sealed class FundPortalService : IFundPortalService
{
    private readonly string _connectionString;
    private readonly ILogger<FundPortalService> _logger;

    public FundPortalService(IConfiguration configuration, ILogger<FundPortalService> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        _logger = logger;
    }

    public async Task<PagedResult<FundListItemDto>> GetFundsAsync(string? search, int page, int pageSize)
    {
        try
        {
            return await GetFundsInternalAsync(search, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get funds cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving funds. Search={Search}, Page={Page}, PageSize={PageSize}", search, page, pageSize);
            throw;
        }
    }

    public async Task<FundDetailDto?> GetFundByKeyAsync(int fundKey)
    {
        try
        {
            return await GetFundByKeyInternalAsync(fundKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get fund {FundKey} cancelled", fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fund {FundKey}", fundKey);
            throw;
        }
    }

    public async Task<IReadOnlyList<FundInvestorDto>> GetFundInvestorsAsync(int fundKey)
    {
        try
        {
            return await GetFundInvestorsInternalAsync(fundKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investors for fund {FundKey} cancelled", fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investors for fund {FundKey}", fundKey);
            throw;
        }
    }

    private async Task<PagedResult<FundListItemDto>> GetFundsInternalAsync(string? search, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append($" from {WarehouseTables.DimFund} f ");
        countSql.Append(" where ");
        WarehouseSql.AppendCurrentFundFilter(countSql, "f");
        WarehouseSql.AppendFundSearchFilter(countSql, "f");

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        countCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" f.fund_key, ");
        sql.Append(" f.fund_name, ");
        sql.Append(" isnull(f.fund_strategy_name, isnull(f.fund_type_name, '')) as category, ");
        sql.Append(" isnull(agg.invested_amount_fmv_total, 0) as current_value, ");
        WarehouseSql.AppendReturnPercentExpression(sql);
        sql.Append(" as total_return_percent ");
        sql.Append($" from {WarehouseTables.DimFund} f ");
        sql.Append(" outer apply ( ");
        sql.Append(" select ");
        sql.Append(" sum(isnull(fi.invested_amount, 0)) as invested_amount_total, ");
        sql.Append(" sum(isnull(fi.invested_amount_fmv, 0)) as invested_amount_fmv_total ");
        sql.Append($" from {WarehouseTables.FactInvestment} fi ");
        sql.Append(" where fi.fund_key = f.fund_key ");
        sql.Append(" ) agg ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        WarehouseSql.AppendFundSearchFilter(sql, "f");
        sql.Append(" order by f.fund_name ");
        sql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        command.Parameters.AddWithValue("@offset", offset);
        command.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<FundListItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var fundName = reader.GetStringOrEmpty("fund_name");
            var category = reader.GetStringOrEmpty("category");
            var currentValue = reader.GetDecimalOrDefault("current_value");
            var totalReturnPercent = reader.GetNullableDecimal("total_return_percent");

            items.Add(new FundListItemDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundName = fundName,
                Category = category,
                CurrentValue = currentValue,
                TotalReturnPercent = totalReturnPercent
            });
        }

        _logger.LogInformation(
            "Retrieved {Count} funds (page {Page}, total {Total}).",
            items.Count, normalizedPage, totalCount);

        return new PagedResult<FundListItemDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private async Task<FundDetailDto?> GetFundByKeyInternalAsync(int fundKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" f.fund_key, ");
        sql.Append(" f.fund_id, ");
        sql.Append(" isnull(f.fund_code, '') as fund_code, ");
        sql.Append(" f.fund_name, ");
        sql.Append(" isnull(f.fund_type_name, 'Fund') as fund_type_name, ");
        sql.Append(" isnull(f.fund_strategy_name, '') as fund_strategy_name, ");
        sql.Append(" case ");
        sql.Append(" when f.dissolution_date is not null then 'Dissolved' ");
        sql.Append(" when isnull(f.is_current, 1) = 1 then 'Active' ");
        sql.Append(" else 'Inactive' ");
        sql.Append(" end as fund_status, ");
        sql.Append(" f.fund_start_date, ");
        sql.Append(" isnull(f.is_sidecar, 0) as is_sidecar, ");
        sql.Append(" isnull(agg.invested_amount_total, 0) as invested_amount_total, ");
        sql.Append(" isnull(agg.invested_amount_fmv_total, 0) as invested_amount_fmv_total, ");
        WarehouseSql.AppendReturnPercentExpression(sql);
        sql.Append(" as total_return_percent, ");
        sql.Append(" isnull(agg.assets_count, 0) as assets_count, ");
        sql.Append(" isnull(agg.investors_count, 0) as investors_count ");
        sql.Append($" from {WarehouseTables.DimFund} f ");
        sql.Append(" outer apply ( ");
        sql.Append(" select ");
        sql.Append(" sum(isnull(fi.invested_amount, 0)) as invested_amount_total, ");
        sql.Append(" sum(isnull(fi.invested_amount_fmv, 0)) as invested_amount_fmv_total, ");
        sql.Append(" ( ");
        sql.Append(" select count(*) ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        WarehouseSql.AppendPropertyBelongsToFundFilter(sql, "p", "f");
        sql.Append(" ) as assets_count, ");
        sql.Append(" count(distinct fi.investor_key) as investors_count ");
        sql.Append($" from {WarehouseTables.FactInvestment} fi ");
        sql.Append(" where fi.fund_key = f.fund_key ");
        sql.Append(" ) agg ");
        sql.Append(" where f.fund_key = @fundKey ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@fundKey", fundKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var resolvedFundKey = reader.GetInt32OrDefault("fund_key");
        var fundId = reader.GetInt32OrDefault("fund_id");
        var fundCode = reader.GetStringOrEmpty("fund_code");
        var fundName = reader.GetStringOrEmpty("fund_name");
        var fundType = reader.GetStringOrEmpty("fund_type_name");
        var fundStrategy = reader.GetStringOrEmpty("fund_strategy_name");
        var status = reader.GetStringOrEmpty("fund_status");
        var fundStartDate = reader.GetNullableDateTime("fund_start_date");
        var isSidecar = reader.GetInt32OrDefault("is_sidecar") == 1;
        var totalInvestedAmount = reader.GetDecimalOrDefault("invested_amount_total");
        var currentValue = reader.GetDecimalOrDefault("invested_amount_fmv_total");
        var totalReturnPercent = reader.GetNullableDecimal("total_return_percent");
        var assetsCount = reader.GetInt32OrDefault("assets_count");
        var investorsCount = reader.GetInt32OrDefault("investors_count");

        var summary = new FundSummaryDto
        {
            FundKey = resolvedFundKey,
            FundId = fundId,
            FundCode = fundCode,
            FundName = fundName,
            FundType = fundType,
            Status = status,
            CurrentValue = currentValue,
            TotalReturnPercent = totalReturnPercent,
            Assets = assetsCount,
            Investors = investorsCount
        };

        var investmentDetails = new List<DynamicFieldDto>
        {
            DisplayFieldBuilder.ToDynamicField("investmentType", DisplayFieldBuilder.Text(fundType)),
            DisplayFieldBuilder.ToDynamicField("strategy", DisplayFieldBuilder.Text(fundStrategy)),
            DisplayFieldBuilder.ToDynamicField("startDate", DisplayFieldBuilder.Date(fundStartDate)),
            DisplayFieldBuilder.ToDynamicField("status", DisplayFieldBuilder.Status(status))
        };

        var financialSummary = new List<DynamicFieldDto>
        {
            DisplayFieldBuilder.ToDynamicField("totalValue", DisplayFieldBuilder.Money(currentValue)),
            DisplayFieldBuilder.ToDynamicField("investedAmount", DisplayFieldBuilder.Money(totalInvestedAmount)),
            DisplayFieldBuilder.ToDynamicField("currentValue", DisplayFieldBuilder.Money(currentValue)),
            DisplayFieldBuilder.ToDynamicField("totalReturn", DisplayFieldBuilder.Percent(totalReturnPercent)),
            DisplayFieldBuilder.ToDynamicField("isSidecar", DisplayFieldBuilder.Boolean(isSidecar))
        };

        return new FundDetailDto
        {
            Summary = summary,
            Sections =
            [
                new DynamicSectionDto
                {
                    Title = "Investment Details",
                    Fields = investmentDetails
                },
                new DynamicSectionDto
                {
                    Title = "Financial Summary",
                    Fields = financialSummary
                }
            ]
        };
    }

    private async Task<IReadOnlyList<FundInvestorDto>> GetFundInvestorsInternalAsync(int fundKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" i.investor_key, ");
        sql.Append(" i.investor_name, ");
        sql.Append(" isnull(i.investor_type_name, '') as investor_type_name, ");
        sql.Append(" case when isnull(i.is_current, 1) = 1 then 'Active' else 'Inactive' end as investor_status, ");
        sql.Append(" i.valid_from as member_since, ");
        sql.Append(" year(i.valid_from) as join_year, ");
        sql.Append(" sum(isnull(f.invested_amount, 0)) as total_invested_amount, ");
        sql.Append(" sum(isnull(f.invested_amount_fmv, 0)) as total_invested_fmv ");
        sql.Append($" from {WarehouseTables.FactInvestment} f ");
        sql.Append($" inner join {WarehouseTables.DimInvestor} i on i.investor_key = f.investor_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
        sql.Append(" where f.fund_key = @fundKey ");
        sql.Append(" group by ");
        sql.Append(" i.investor_key, ");
        sql.Append(" i.investor_name, ");
        sql.Append(" i.investor_type_name, ");
        sql.Append(" i.is_current, ");
        sql.Append(" i.valid_from ");
        sql.Append(" order by i.investor_name ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@fundKey", fundKey);

        var items = new List<FundInvestorDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var memberSince = reader.GetNullableDateTime("member_since");
            var joinYearOrdinal = reader.GetOrdinal("join_year");
            int? joinYear = reader.IsDBNull(joinYearOrdinal) ? null : Convert.ToInt32(reader.GetValue(joinYearOrdinal));

            var investorKey = reader.GetInt64OrDefault("investor_key");
            var investorName = reader.GetStringOrEmpty("investor_name");
            var investorType = reader.GetStringOrEmpty("investor_type_name");
            var status = reader.GetStringOrEmpty("investor_status");
            var totalInvestedFmv = reader.GetDecimalOrDefault("total_invested_fmv");

            items.Add(new FundInvestorDto
            {
                InvestorKey = investorKey,
                InvestorName = investorName,
                InvestorType = investorType,
                Status = status,
                TotalInvested = totalInvestedFmv,
                TotalInvestedFmv = totalInvestedFmv,
                MemberSince = memberSince,
                JoinYear = joinYear
            });
        }

        return items;
    }
}
