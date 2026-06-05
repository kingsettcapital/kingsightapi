using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class InvestorPortalService : IInvestorPortalService
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

    public async Task<PagedResult<InvestorInvestmentDto>> GetInvestorFundsAsync(long investorKey, int page, int pageSize)
    {
        try
        {
            return await GetInvestorFundsInternalAsync(investorKey, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get funds for investor {InvestorKey} cancelled", investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving funds for investor {InvestorKey}", investorKey);
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
        countSql.Append(" from ( ");
        countSql.Append(" select b.investor_name ");
        AppendInvestorLtdListingFrom(countSql);
        countSql.Append(" group by b.investor_name ");
        countSql.Append(" ) investor_rows ");

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        countCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" min(b.investor_key) as investor_key, ");
        pageSql.Append(" b.investor_name, ");
        pageSql.Append(" max(isnull(b.investor_type_name, '')) as investor_type_name, ");
        pageSql.Append(" sum(isnull(a.net_invested_capital_amount, 0)) as total_invested ");
        AppendInvestorLtdListingFrom(pageSql);
        pageSql.Append(" group by b.investor_name ");
        pageSql.Append(" order by b.investor_name ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        pageCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<InvestorListItemDto>();
        await using (var pageReader = await pageCommand.ExecuteReaderAsync())
        {
            while (await pageReader.ReadAsync())
            {
                items.Add(new InvestorListItemDto
                {
                    InvestorKey = pageReader.GetInt64OrDefault("investor_key"),
                    InvestorName = pageReader.GetStringOrEmpty("investor_name"),
                    InvestorType = pageReader.GetStringOrEmpty("investor_type_name"),
                    TotalInvested = pageReader.GetDecimalOrDefault("total_invested")
                });
            }
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

    private static void AppendInvestorLtdListingFrom(StringBuilder sql)
    {
        sql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} a ");
        sql.Append($" inner join {WarehouseTables.DimInvestor} b on a.investor_key = b.investor_key ");
        sql.Append(" where b.is_current = 1 ");
        WarehouseSql.AppendInvestorSearchFilter(sql, "b");
    }

    private async Task<InvestorDetailDto?> GetInvestorByKeyInternalAsync(long investorKey)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var investorSql = new StringBuilder();
        investorSql.Append(" select ");
        investorSql.Append(" i.investor_key, ");
        investorSql.Append(" i.investor_id, ");
        investorSql.Append(" i.investor_name, ");
        investorSql.Append(" isnull(i.investor_short_name, '') as investor_short_name, ");
        investorSql.Append(" isnull(i.relationship_name, '') as relationship_name, ");
        investorSql.Append(" isnull(i.investor_type_name, '') as investor_type_name, ");
        investorSql.Append(" case when isnull(i.is_current, 1) = 1 then 'Active' else 'Inactive' end as investor_status, ");
        investorSql.Append(" isnull(i.address_line1, '') as address_line1, ");
        investorSql.Append(" isnull(i.address_line2, '') as address_line2, ");
        investorSql.Append(" isnull(i.city, '') as city, ");
        investorSql.Append(" isnull(i.province, '') as province, ");
        investorSql.Append(" isnull(i.country, '') as country, ");
        investorSql.Append(" isnull(i.contact_first_name, '') as contact_first_name, ");
        investorSql.Append(" isnull(i.contact_last_name, '') as contact_last_name, ");
        investorSql.Append(" isnull(i.contact_email, '') as contact_email, ");
        investorSql.Append(" i.valid_from as member_since ");
        investorSql.Append($" from {WarehouseTables.DimInvestor} i ");
        investorSql.Append(" where i.investor_key = @investorKey ");
        investorSql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(investorSql, "i");

        await using var investorCommand = new SqlCommand(investorSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        investorCommand.Parameters.AddWithValue("@investorKey", investorKey);

        await using var investorReader = await investorCommand.ExecuteReaderAsync();
        if (!await investorReader.ReadAsync())
        {
            return null;
        }

        var memberSince = investorReader.GetNullableDateTime("member_since");
        var resolvedInvestorKey = investorReader.GetInt64OrDefault("investor_key");
        var investorId = investorReader.GetInt32OrDefault("investor_id");
        var investorName = investorReader.GetStringOrEmpty("investor_name");
        var investorShortName = investorReader.GetStringOrEmpty("investor_short_name");
        var relationshipName = investorReader.GetStringOrEmpty("relationship_name");
        var investorType = investorReader.GetStringOrEmpty("investor_type_name");
        var status = investorReader.GetStringOrEmpty("investor_status");
        var addressLine1 = investorReader.GetStringOrEmpty("address_line1");
        var addressLine2 = investorReader.GetStringOrEmpty("address_line2");
        var city = investorReader.GetStringOrEmpty("city");
        var province = investorReader.GetStringOrEmpty("province");
        var country = investorReader.GetStringOrEmpty("country");
        var contactFirstName = investorReader.GetStringOrEmpty("contact_first_name");
        var contactLastName = investorReader.GetStringOrEmpty("contact_last_name");
        var contactEmail = investorReader.GetStringOrEmpty("contact_email");

        var aggSql = new StringBuilder();
        aggSql.Append(" select ");
        aggSql.Append(" isnull(( ");
        aggSql.Append(" select sum(isnull(fc.committed_amount, 0)) ");
        aggSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        aggSql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fc.fund_key ");
        aggSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(aggSql, "df");
        aggSql.Append(" where fc.investor_key = @investorKey ");
        aggSql.Append(" ), 0) as total_committed_value, ");
        aggSql.Append(" isnull(( ");
        aggSql.Append(" select sum(isnull(p.net_invested_capital_amount, 0)) ");
        aggSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} p ");
        aggSql.Append($" inner join {WarehouseTables.DimInvestor} i2 on i2.investor_key = p.investor_key ");
        aggSql.Append(" where i2.investor_key = @investorKey and i2.is_current = 1 ");
        aggSql.Append(" ), 0) as total_invested_value, ");
        aggSql.Append(" isnull(( ");
        aggSql.Append(" select sum(case when lower(isnull(df.fund_type_name, '')) = 'unitized' ");
        aggSql.Append(" then isnull(fi.invested_units, 0) else isnull(fi.invested_amount, 0) end) ");
        aggSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        aggSql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fi.fund_key ");
        aggSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(aggSql, "df");
        aggSql.Append(" where fi.investor_key = @investorKey ");
        aggSql.Append(" ), 0) as total_current_value, ");
        aggSql.Append(" isnull(( ");
        aggSql.Append(" select count(*) from ( ");
        aggSql.Append($" select distinct fund_key from {WarehouseTables.FactCommitted} where investor_key = @investorKey ");
        aggSql.Append(" union ");
        aggSql.Append($" select distinct fund_key from {WarehouseTables.FactInvestment} where investor_key = @investorKey ");
        aggSql.Append(" ) funds ");
        aggSql.Append(" ), 0) as investments_count, ");
        aggSql.Append(" isnull(( ");
        aggSql.Append(" select count(distinct fi2.fund_key) ");
        aggSql.Append($" from {WarehouseTables.FactInvestment} fi2 ");
        aggSql.Append(" where fi2.investor_key = @investorKey and isnull(fi2.invested_amount, 0) <> 0 ");
        aggSql.Append(" ), 0) as active_investments_count, ");
        aggSql.Append(" ( ");
        aggSql.Append(" select min(try_convert(date, cast(fi3.calculation_date_key as varchar(8)), 112)) ");
        aggSql.Append($" from {WarehouseTables.FactInvestment} fi3 where fi3.investor_key = @investorKey ");
        aggSql.Append(" ) as first_investment_date ");

        await using var aggCommand = new SqlCommand(aggSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        aggCommand.Parameters.AddWithValue("@investorKey", investorKey);

        await investorReader.DisposeAsync();

        await using var aggReader = await aggCommand.ExecuteReaderAsync();
        if (!await aggReader.ReadAsync())
        {
            return null;
        }

        var totalCommittedValue = aggReader.GetDecimalOrDefault("total_committed_value");
        var totalInvestedValue = aggReader.GetDecimalOrDefault("total_invested_value");
        var investmentsCount = aggReader.GetInt32OrDefault("investments_count");
        var activeInvestmentsCount = aggReader.GetInt32OrDefault("active_investments_count");
        var firstInvestmentDate = aggReader.GetNullableDateTime("first_investment_date");

        int? joinYear = null;
        var effectiveYearSource = firstInvestmentDate ?? memberSince;
        if (effectiveYearSource.HasValue)
        {
            joinYear = effectiveYearSource.Value.Year;
        }

        var summary = new InvestorSummaryDto
        {
            InvestorKey = resolvedInvestorKey,
            InvestorId = investorId,
            InvestorName = investorName,
            InvestorType = investorType,
            Status = status,
            TotalInvested = totalInvestedValue,
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

    private async Task<PagedResult<InvestorInvestmentDto>> GetInvestorFundsInternalAsync(long investorKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        AppendInvestorFundsBaseSelect(countSql);
        countSql.Append(" ) fund_rows ");

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        countCommand.Parameters.AddWithValue("@investorKey", investorKey);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var fundsSql = new StringBuilder();
        AppendInvestorFundsBaseSelect(fundsSql);
        fundsSql.Append(" order by df.fund_code ");
        fundsSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        var funds = new List<(int FundKey, string FundCode, string FundName, string FundType, string FundCategory, string Status)>();
        await using (var fundsCommand = new SqlCommand(fundsSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        })
        {
            fundsCommand.Parameters.AddWithValue("@investorKey", investorKey);
            fundsCommand.Parameters.AddWithValue("@offset", offset);
            fundsCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

            await using var fundsReader = await fundsCommand.ExecuteReaderAsync();
            while (await fundsReader.ReadAsync())
            {
                funds.Add((
                    fundsReader.GetInt32OrDefault("fund_key"),
                    fundsReader.GetStringOrEmpty("fund_code"),
                    fundsReader.GetStringOrEmpty("fund_name"),
                    fundsReader.GetStringOrEmpty("fund_type"),
                    fundsReader.GetStringOrEmpty("fund_category"),
                    fundsReader.GetStringOrEmpty("fund_status")
                ));
            }
        }

        if (funds.Count == 0)
        {
            return new PagedResult<InvestorInvestmentDto>
            {
                Items = [],
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount
            };
        }

        var fundCodeParameters = new List<string>();
        for (var i = 0; i < funds.Count; i++)
        {
            fundCodeParameters.Add($"@fundCode{i}");
        }

        var aggregateSql = new StringBuilder();
        aggregateSql.Append(" select ");
        aggregateSql.Append(" x.fund_code, ");
        aggregateSql.Append(" isnull(comm.invested_amount_total, 0) as invested_amount_total, ");
        aggregateSql.Append(" isnull(inv.invested_amount_fmv_total, 0) as invested_amount_fmv_total, ");
        aggregateSql.Append(" inv.total_return_percent as total_return_percent ");
        aggregateSql.Append(" from ( ");
        aggregateSql.Append(" select distinct isnull(df2.fund_code, '') as fund_code ");
        aggregateSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        aggregateSql.Append($" inner join {WarehouseTables.DimFund} df2 on df2.fund_key = fc.fund_key ");
        aggregateSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(aggregateSql, "df2");
        aggregateSql.Append(" where fc.investor_key = @investorKey ");
        aggregateSql.Append(" union ");
        aggregateSql.Append(" select distinct isnull(df2.fund_code, '') as fund_code ");
        aggregateSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        aggregateSql.Append($" inner join {WarehouseTables.DimFund} df2 on df2.fund_key = fi.fund_key ");
        aggregateSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(aggregateSql, "df2");
        aggregateSql.Append(" where fi.investor_key = @investorKey ");
        aggregateSql.Append(" ) x ");
        aggregateSql.Append(" left join ( ");
        aggregateSql.Append(" select isnull(df3.fund_code, '') as fund_code, sum(isnull(fc.committed_amount, 0)) as invested_amount_total ");
        aggregateSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        aggregateSql.Append($" inner join {WarehouseTables.DimFund} df3 on df3.fund_key = fc.fund_key ");
        aggregateSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(aggregateSql, "df3");
        aggregateSql.Append(" where fc.investor_key = @investorKey ");
        aggregateSql.Append(" group by df3.fund_code ");
        aggregateSql.Append(" ) comm on comm.fund_code = x.fund_code ");
        aggregateSql.Append(" left join ( ");
        aggregateSql.Append(" select ");
        aggregateSql.Append(" isnull(df4.fund_code, '') as fund_code, ");
        aggregateSql.Append(" ( ");
        aggregateSql.Append(" sum(CASE WHEN lower(isnull(df4.fund_type_name, '')) = 'unitized' ");
        aggregateSql.Append("     THEN isnull(fi.invested_units, 0) ELSE 0 END) ");
        aggregateSql.Append(" + ");
        aggregateSql.Append(" sum(CASE WHEN lower(isnull(df4.fund_type_name, '')) <> 'unitized' ");
        aggregateSql.Append("     THEN isnull(fi.invested_amount, 0) ELSE 0 END) ");
        aggregateSql.Append(" ) as invested_amount_fmv_total, ");
        aggregateSql.Append(" case ");
        aggregateSql.Append(" when abs(sum(isnull(fi.invested_amount, 0))) > 0 ");
        aggregateSql.Append(" then ( (sum(isnull(fi.invested_amount_fmv, 0)) - sum(isnull(fi.invested_amount, 0))) ");
        aggregateSql.Append("      / abs(sum(isnull(fi.invested_amount, 0))) ) * 100.0 ");
        aggregateSql.Append(" else null ");
        aggregateSql.Append(" end as total_return_percent ");
        aggregateSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        aggregateSql.Append($" inner join {WarehouseTables.DimFund} df4 on df4.fund_key = fi.fund_key ");
        aggregateSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(aggregateSql, "df4");
        aggregateSql.Append(" where fi.investor_key = @investorKey ");
        aggregateSql.Append(" group by df4.fund_code ");
        aggregateSql.Append(" ) inv on inv.fund_code = x.fund_code ");
        aggregateSql.Append($" where x.fund_code in ({string.Join(", ", fundCodeParameters)}) ");

        var totalsByFundCode = new Dictionary<string, (decimal InvestedAmountTotal, decimal InvestedAmountFmvTotal, decimal? TotalReturnPercent)>(StringComparer.OrdinalIgnoreCase);
        await using (var aggregateCommand = new SqlCommand(aggregateSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        })
        {
            aggregateCommand.Parameters.AddWithValue("@investorKey", investorKey);
            for (var i = 0; i < funds.Count; i++)
            {
                aggregateCommand.Parameters.AddWithValue(fundCodeParameters[i], funds[i].FundCode);
            }

            await using var aggregateReader = await aggregateCommand.ExecuteReaderAsync();
            while (await aggregateReader.ReadAsync())
            {
                var fundCode = aggregateReader.GetStringOrEmpty("fund_code");
                totalsByFundCode[fundCode] = (
                    aggregateReader.GetDecimalOrDefault("invested_amount_total"),
                    aggregateReader.GetDecimalOrDefault("invested_amount_fmv_total"),
                    aggregateReader.GetNullableDecimal("total_return_percent")
                );
            }
        }

        var items = new List<InvestorInvestmentDto>();
        foreach (var fund in funds)
        {
            totalsByFundCode.TryGetValue(fund.FundCode, out var totals);

            items.Add(new InvestorInvestmentDto
            {
                FundKey = fund.FundKey,
                FundCode = fund.FundCode,
                FundName = fund.FundName,
                FundType = fund.FundType,
                FundCategory = fund.FundCategory,
                Status = fund.Status,
                InvestedAmount = totals.InvestedAmountTotal,
                InvestedAmountFmv = totals.InvestedAmountFmvTotal,
                TotalReturnPercent = totals.TotalReturnPercent
            });
        }

        return new PagedResult<InvestorInvestmentDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private static void AppendInvestorFundsBaseSelect(StringBuilder sql)
    {
        sql.Append(" select ");
        sql.Append(" min(df.fund_key) as fund_key, ");
        sql.Append(" isnull(df.fund_code, '') as fund_code, ");
        sql.Append(" max(isnull(df.fund_name, '')) as fund_name, ");
        sql.Append(" max(isnull(df.fund_type_name, '')) as fund_type, ");
        sql.Append(" max(isnull(df.fund_strategy_name, isnull(df.fund_type_name, ''))) as fund_category, ");
        sql.Append(" case ");
        sql.Append(" when max(case when df.dissolution_date is not null then 1 else 0 end) = 1 then 'Dissolved' ");
        sql.Append(" when max(case when isnull(df.is_current, 1) = 1 then 1 else 0 end) = 1 then 'Active' ");
        sql.Append(" else 'Inactive' ");
        sql.Append(" end as fund_status ");
        sql.Append(" from ( ");
        sql.Append($" select distinct fund_key from {WarehouseTables.FactCommitted} where investor_key = @investorKey ");
        sql.Append(" union ");
        sql.Append($" select distinct fund_key from {WarehouseTables.FactInvestment} where investor_key = @investorKey ");
        sql.Append(" ) fk ");
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fk.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
        sql.Append(" group by df.fund_code ");
    }
}
