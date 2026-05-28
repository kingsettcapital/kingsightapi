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
        sql.Append(" isnull(agg.total_committed_value, 0) as total_invested ");
        sql.Append($" from {WarehouseTables.DimInvestor} i ");
        sql.Append(" outer apply ( ");
        sql.Append(" select ");
        sql.Append(" sum(isnull(fc.committed_amount, 0)) as total_committed_value ");
        sql.Append($" from {WarehouseTables.FactCommitted} fc ");
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fc.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
        sql.Append(" where fc.investor_key = i.investor_key ");
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
        sql.Append(" isnull(i.contact_first_name, '') as contact_first_name, ");
        sql.Append(" isnull(i.contact_last_name, '') as contact_last_name, ");
        sql.Append(" isnull(i.contact_email, '') as contact_email, ");
        sql.Append(" i.valid_from as member_since, ");
        sql.Append(" isnull(( ");
        sql.Append(" select sum(isnull(fc.committed_amount, 0)) ");
        sql.Append($" from {WarehouseTables.FactCommitted} fc ");
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fc.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
        sql.Append(" where fc.investor_key = i.investor_key ");
        sql.Append(" ), 0) as total_committed_value, ");
        sql.Append(" isnull(( ");
        sql.Append(" select sum(case when lower(isnull(df.fund_type_name, '')) = 'unitized' then isnull(fi.invested_units, 0) else isnull(fi.invested_amount, 0) end) ");
        sql.Append($" from {WarehouseTables.FactInvestment} fi ");
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fi.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
        sql.Append(" where fi.investor_key = i.investor_key ");
        sql.Append(" ), 0) as total_current_value, ");
        sql.Append(" isnull(( ");
        sql.Append(" select count(*) from ( ");
        sql.Append($" select distinct fund_key from {WarehouseTables.FactCommitted} where investor_key = i.investor_key ");
        sql.Append(" union ");
        sql.Append($" select distinct fund_key from {WarehouseTables.FactInvestment} where investor_key = i.investor_key ");
        sql.Append(" ) funds ");
        sql.Append(" ), 0) as investments_count, ");
        sql.Append(" isnull(( ");
        sql.Append(" select count(distinct fi2.fund_key) ");
        sql.Append($" from {WarehouseTables.FactInvestment} fi2 where fi2.investor_key = i.investor_key and isnull(fi2.invested_amount, 0) <> 0 ");
        sql.Append(" ), 0) as active_investments_count, ");
        sql.Append(" year(coalesce(( ");
        sql.Append(" select min(try_convert(date, cast(fi3.calculation_date_key as varchar(8)), 112)) ");
        sql.Append($" from {WarehouseTables.FactInvestment} fi3 where fi3.investor_key = i.investor_key ");
        sql.Append(" ), i.valid_from)) as first_investment_year ");
        sql.Append($" from {WarehouseTables.DimInvestor} i ");
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
        var totalCommittedValue = reader.GetDecimalOrDefault("total_committed_value");
        var investmentsCount = reader.GetInt32OrDefault("investments_count");
        var activeInvestmentsCount = reader.GetInt32OrDefault("active_investments_count");
        var addressLine1 = reader.GetStringOrEmpty("address_line1");
        var addressLine2 = reader.GetStringOrEmpty("address_line2");
        var city = reader.GetStringOrEmpty("city");
        var province = reader.GetStringOrEmpty("province");
        var country = reader.GetStringOrEmpty("country");
        var contactFirstName = reader.GetStringOrEmpty("contact_first_name");
        var contactLastName = reader.GetStringOrEmpty("contact_last_name");
        var contactEmail = reader.GetStringOrEmpty("contact_email");

        var summary = new InvestorSummaryDto
        {
            InvestorKey = resolvedInvestorKey,
            InvestorId = investorId,
            InvestorName = investorName,
            InvestorType = investorType,
            Status = status,
            TotalInvested = totalCommittedValue,
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
            DisplayFieldBuilder.ToDynamicField("contactFirstName", DisplayFieldBuilder.Text(contactFirstName)),
            DisplayFieldBuilder.ToDynamicField("contactLastName", DisplayFieldBuilder.Text(contactLastName)),
            DisplayFieldBuilder.ToDynamicField("contactEmail", DisplayFieldBuilder.Text(contactEmail)),
            DisplayFieldBuilder.ToDynamicField("contactPhone", DisplayFieldBuilder.Text(string.Empty)),
            DisplayFieldBuilder.ToDynamicField("memberSince", DisplayFieldBuilder.Date(memberSince))
        };

        var portfolioSummary = new List<DynamicFieldDto>
        {
            DisplayFieldBuilder.ToDynamicField("activeInvestmentsCount", DisplayFieldBuilder.Integer(activeInvestmentsCount)),
            DisplayFieldBuilder.ToDynamicField("investmentsCount", DisplayFieldBuilder.Integer(investmentsCount)),
            DisplayFieldBuilder.ToDynamicField("totalCommitted", DisplayFieldBuilder.Money(totalCommittedValue)),
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
        sql.Append(" df.fund_key, ");
        sql.Append(" isnull(df.fund_name, '') as fund_name, ");
        sql.Append(" isnull(df.fund_type_name, '') as fund_type, ");
        sql.Append(" isnull(df.fund_strategy_name, isnull(df.fund_type_name, '')) as fund_category, ");
        sql.Append(" case ");
        sql.Append(" when df.dissolution_date is not null then 'Dissolved' ");
        sql.Append(" when isnull(df.is_current, 1) = 1 then 'Active' ");
        sql.Append(" else 'Inactive' ");
        sql.Append(" end as fund_status, ");
        sql.Append(" isnull(committed.invested_amount_total, 0) as invested_amount_total, ");
        sql.Append(" isnull(currentvals.invested_amount_fmv_total, 0) as invested_amount_fmv_total, ");
        sql.Append(" case ");
        sql.Append(" when abs(isnull(currentvals.return_amount_total, 0)) > 0 ");
        sql.Append(" then ((isnull(currentvals.return_amount_fmv_total, 0) - isnull(currentvals.return_amount_total, 0)) / abs(currentvals.return_amount_total)) * 100.0 ");
        sql.Append(" else null ");
        sql.Append(" end as total_return_percent ");
        sql.Append(" from ( ");
        sql.Append($" select distinct fund_key from {WarehouseTables.FactCommitted} where investor_key = @investorKey ");
        sql.Append(" union ");
        sql.Append($" select distinct fund_key from {WarehouseTables.FactInvestment} where investor_key = @investorKey ");
        sql.Append(" ) fk ");
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fk.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
        sql.Append(" outer apply ( ");
        sql.Append(" select sum(isnull(fc.committed_amount, 0)) as invested_amount_total ");
        sql.Append($" from {WarehouseTables.FactCommitted} fc where fc.investor_key = @investorKey and fc.fund_key = df.fund_key ");
        sql.Append(" ) committed ");
        sql.Append(" outer apply ( ");
        sql.Append(" select ");
        sql.Append(" sum(case when lower(isnull(df.fund_type_name, '')) = 'unitized' then isnull(fi.invested_units, 0) else isnull(fi.invested_amount, 0) end) as invested_amount_fmv_total, ");
        sql.Append(" sum(isnull(fi.invested_amount, 0)) as return_amount_total, ");
        sql.Append(" sum(isnull(fi.invested_amount_fmv, 0)) as return_amount_fmv_total ");
        sql.Append($" from {WarehouseTables.FactInvestment} fi where fi.investor_key = @investorKey and fi.fund_key = df.fund_key ");
        sql.Append(" ) currentvals ");
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
