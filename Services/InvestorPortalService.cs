using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public interface IInvestorPortalService
{
    Task<PagedResult<InvestorListItemDto>> GetInvestorsAsync(string? search, int page, int pageSize);
    Task<InvestorDetailDto?> GetInvestorByKeyAsync(long investorKey);
    Task<IReadOnlyList<InvestorInvestmentDto>> GetInvestorInvestmentsAsync(long investorKey);
}

public sealed class InvestorPortalService : IInvestorPortalService
{
    private readonly string _connectionString;
    private readonly ILogger<InvestorPortalService> _logger;

    public InvestorPortalService(IConfiguration configuration, ILogger<InvestorPortalService> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        _logger = logger;
    }

    public async Task<PagedResult<InvestorListItemDto>> GetInvestorsAsync(string? search, int page, int pageSize)
    {
        try
        {
            return await GetInvestorsInternalAsync(search, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investors cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investors. Search={Search}, Page={Page}, PageSize={PageSize}", search, page, pageSize);
            throw;
        }
    }

    public async Task<InvestorDetailDto?> GetInvestorByKeyAsync(long investorKey)
    {
        try
        {
            return await GetInvestorByKeyInternalAsync(investorKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investor {InvestorKey} cancelled", investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investor {InvestorKey}", investorKey);
            throw;
        }
    }

    public async Task<IReadOnlyList<InvestorInvestmentDto>> GetInvestorInvestmentsAsync(long investorKey)
    {
        try
        {
            return await GetInvestorInvestmentsInternalAsync(investorKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investments for investor {InvestorKey} cancelled", investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investments for investor {InvestorKey}", investorKey);
            throw;
        }
    }

    private async Task<PagedResult<InvestorListItemDto>> GetInvestorsInternalAsync(string? search, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append($" from {WarehouseTables.DimInvestor} i ");
        countSql.Append(" where ");
        WarehouseSql.AppendCurrentInvestorFilter(countSql, "i");
        WarehouseSql.AppendInvestorSearchFilter(countSql, "i");

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        countCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" i.investor_key, ");
        sql.Append(" i.investor_name, ");
        sql.Append(" isnull(i.investor_type_name, '') as investor_type_name, ");
        sql.Append(" isnull(agg.total_invested_fmv, 0) as total_invested ");
        sql.Append($" from {WarehouseTables.DimInvestor} i ");
        sql.Append(" outer apply ( ");
        sql.Append(" select sum(isnull(f.invested_amount_fmv, 0)) as total_invested_fmv ");
        sql.Append($" from {WarehouseTables.FactInvestment} f ");
        sql.Append(" where f.investor_key = i.investor_key ");
        sql.Append(" ) agg ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
        WarehouseSql.AppendInvestorSearchFilter(sql, "i");
        sql.Append(" order by i.investor_name ");
        sql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        command.Parameters.AddWithValue("@offset", offset);
        command.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<InvestorListItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var investorName = reader.GetStringOrEmpty("investor_name");
            var investorType = reader.GetStringOrEmpty("investor_type_name");
            var totalInvested = reader.GetDecimalOrDefault("total_invested");

            items.Add(new InvestorListItemDto
            {
                InvestorKey = reader.GetInt64OrDefault("investor_key"),
                InvestorName = investorName,
                InvestorType = investorType,
                TotalInvested = totalInvested
            });
        }

        _logger.LogInformation(
            "Retrieved {Count} investors (page {Page}, total {Total}).",
            items.Count, normalizedPage, totalCount);

        return new PagedResult<InvestorListItemDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private async Task<InvestorDetailDto?> GetInvestorByKeyInternalAsync(long investorKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" i.investor_key, ");
        sql.Append(" i.investor_id, ");
        sql.Append(" i.investor_name, ");
        sql.Append(" isnull(i.investor_short_name, '') as investor_short_name, ");
        sql.Append(" isnull(i.relationship_name, '') as relationship_name, ");
        sql.Append(" isnull(i.investor_type_name, '') as investor_type_name, ");
        sql.Append(" case when isnull(i.is_current, 1) = 1 then 'Active' else 'Inactive' end as investor_status, ");
        sql.Append(" isnull(i.address_line1, '') as address_line1, ");
        sql.Append(" isnull(i.address_line2, '') as address_line2, ");
        sql.Append(" isnull(i.city, '') as city, ");
        sql.Append(" isnull(i.province, '') as province, ");
        sql.Append(" isnull(i.country, '') as country, ");
        sql.Append(" i.valid_from as member_since, ");
        sql.Append(" isnull(agg.total_invested_amount, 0) as total_invested_amount, ");
        sql.Append(" isnull(agg.total_invested_fmv, 0) as total_invested_fmv, ");
        sql.Append(" isnull(agg.investments_count, 0) as investments_count, ");
        sql.Append(" isnull(agg.active_investments_count, 0) as active_investments_count, ");
        sql.Append(" year(coalesce(agg.first_investment_date, i.valid_from)) as first_investment_year ");
        sql.Append($" from {WarehouseTables.DimInvestor} i ");
        sql.Append(" outer apply ( ");
        sql.Append(" select ");
        sql.Append(" sum(isnull(f.invested_amount, 0)) as total_invested_amount, ");
        sql.Append(" sum(isnull(f.invested_amount_fmv, 0)) as total_invested_fmv, ");
        sql.Append(" count(distinct f.fund_key) as investments_count, ");
        sql.Append(" count(distinct case when isnull(f.invested_amount_fmv, 0) <> 0 then f.fund_key end) as active_investments_count, ");
        sql.Append(" min(try_convert(date, cast(f.calculation_date_key as varchar(8)), 112)) as first_investment_date ");
        sql.Append($" from {WarehouseTables.FactInvestment} f ");
        sql.Append(" where f.investor_key = i.investor_key ");
        sql.Append(" ) agg ");
        sql.Append(" where i.investor_key = @investorKey ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@investorKey", investorKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var memberSince = reader.GetNullableDateTime("member_since");
        var joinYearOrdinal = reader.GetOrdinal("first_investment_year");
        int? joinYear = reader.IsDBNull(joinYearOrdinal) ? null : Convert.ToInt32(reader.GetValue(joinYearOrdinal));

        var resolvedInvestorKey = reader.GetInt64OrDefault("investor_key");
        var investorId = reader.GetInt32OrDefault("investor_id");
        var investorName = reader.GetStringOrEmpty("investor_name");
        var investorShortName = reader.GetStringOrEmpty("investor_short_name");
        var relationshipName = reader.GetStringOrEmpty("relationship_name");
        var investorType = reader.GetStringOrEmpty("investor_type_name");
        var status = reader.GetStringOrEmpty("investor_status");
        var totalInvestedFmv = reader.GetDecimalOrDefault("total_invested_fmv");
        var investmentsCount = reader.GetInt32OrDefault("investments_count");
        var activeInvestmentsCount = reader.GetInt32OrDefault("active_investments_count");
        var addressLine1 = reader.GetStringOrEmpty("address_line1");
        var addressLine2 = reader.GetStringOrEmpty("address_line2");
        var city = reader.GetStringOrEmpty("city");
        var province = reader.GetStringOrEmpty("province");
        var country = reader.GetStringOrEmpty("country");

        var summary = new InvestorSummaryDto
        {
            InvestorKey = resolvedInvestorKey,
            InvestorId = investorId,
            InvestorName = investorName,
            InvestorType = investorType,
            Status = status,
            TotalInvested = totalInvestedFmv,
            InvestmentsCount = investmentsCount,
            DocumentsCount = 0,
            JoinYear = joinYear
        };

        var contactInformation = new List<DynamicFieldDto>
        {
            DisplayFieldBuilder.ToDynamicField("addressLine1", DisplayFieldBuilder.Text(addressLine1)),
            DisplayFieldBuilder.ToDynamicField("addressLine2", DisplayFieldBuilder.Text(addressLine2)),
            DisplayFieldBuilder.ToDynamicField("city", DisplayFieldBuilder.Text(city)),
            DisplayFieldBuilder.ToDynamicField("province", DisplayFieldBuilder.Text(province)),
            DisplayFieldBuilder.ToDynamicField("country", DisplayFieldBuilder.Text(country)),
            DisplayFieldBuilder.ToDynamicField("memberSince", DisplayFieldBuilder.Date(memberSince))
        };

        var portfolioSummary = new List<DynamicFieldDto>
        {
            DisplayFieldBuilder.ToDynamicField("activeInvestmentsCount", DisplayFieldBuilder.Integer(activeInvestmentsCount)),
            DisplayFieldBuilder.ToDynamicField("investmentsCount", DisplayFieldBuilder.Integer(investmentsCount)),
            DisplayFieldBuilder.ToDynamicField("totalCommitted", DisplayFieldBuilder.Money(totalInvestedFmv)),
            DisplayFieldBuilder.ToDynamicField("investorType", DisplayFieldBuilder.Text(investorType)),
            DisplayFieldBuilder.ToDynamicField("relationshipName", DisplayFieldBuilder.Text(relationshipName)),
            DisplayFieldBuilder.ToDynamicField("investorShortName", DisplayFieldBuilder.Text(investorShortName))
        };

        return new InvestorDetailDto
        {
            Summary = summary,
            Sections =
            [
                new DynamicSectionDto
                {
                    Title = "Contact Information",
                    Fields = contactInformation
                },
                new DynamicSectionDto
                {
                    Title = "Portfolio Summary",
                    Fields = portfolioSummary
                }
            ]
        };
    }

    private async Task<IReadOnlyList<InvestorInvestmentDto>> GetInvestorInvestmentsInternalAsync(long investorKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" f.fund_key, ");
        sql.Append(" isnull(df.fund_name, '') as fund_name, ");
        sql.Append(" isnull(df.fund_type_name, '') as fund_type, ");
        sql.Append(" isnull(df.fund_strategy_name, isnull(df.fund_type_name, '')) as fund_category, ");
        sql.Append(" case ");
        sql.Append(" when df.dissolution_date is not null then 'Dissolved' ");
        sql.Append(" when isnull(df.is_current, 1) = 1 then 'Active' ");
        sql.Append(" else 'Inactive' ");
        sql.Append(" end as fund_status, ");
        sql.Append(" sum(isnull(f.invested_amount, 0)) as invested_amount_total, ");
        sql.Append(" sum(isnull(f.invested_amount_fmv, 0)) as invested_amount_fmv_total, ");
        WarehouseSql.AppendReturnPercentFromFactSums(sql, "f");
        sql.Append(" as total_return_percent ");
        sql.Append($" from {WarehouseTables.FactInvestment} f ");
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = f.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
        sql.Append(" where f.investor_key = @investorKey ");
        sql.Append(" group by ");
        sql.Append(" f.fund_key, ");
        sql.Append(" df.fund_name, ");
        sql.Append(" df.fund_strategy_name, ");
        sql.Append(" df.fund_type_name, ");
        sql.Append(" df.dissolution_date, ");
        sql.Append(" df.is_current ");
        sql.Append(" order by df.fund_name ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@investorKey", investorKey);

        var items = new List<InvestorInvestmentDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var fundKey = reader.GetInt32OrDefault("fund_key");
            var fundName = reader.GetStringOrEmpty("fund_name");
            var fundType = reader.GetStringOrEmpty("fund_type");
            var fundCategory = reader.GetStringOrEmpty("fund_category");
            var status = reader.GetStringOrEmpty("fund_status");
            var investedAmount = reader.GetDecimalOrDefault("invested_amount_total");
            var investedAmountFmv = reader.GetDecimalOrDefault("invested_amount_fmv_total");
            var totalReturnPercent = reader.GetNullableDecimal("total_return_percent");

            items.Add(new InvestorInvestmentDto
            {
                FundKey = fundKey,
                FundName = fundName,
                FundType = fundType,
                FundCategory = fundCategory,
                Status = status,
                InvestedAmount = investedAmount,
                InvestedAmountFmv = investedAmountFmv,
                TotalReturnPercent = totalReturnPercent
            });
        }

        return items;
    }
}
