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

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" i.investor_key, ");
        pageSql.Append(" i.investor_name, ");
        pageSql.Append(" isnull(i.investor_type_name, '') as investor_type_name ");
        pageSql.Append($" from {WarehouseTables.DimInvestor} i ");
        pageSql.Append(" where ");
        WarehouseSql.AppendCurrentInvestorFilter(pageSql, "i");
        WarehouseSql.AppendInvestorSearchFilter(pageSql, "i");
        pageSql.Append(" order by i.investor_name ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        pageCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var pageRows = new List<(long InvestorKey, string InvestorName, string InvestorType)>();
        await using (var pageReader = await pageCommand.ExecuteReaderAsync())
        {
            while (await pageReader.ReadAsync())
            {
                pageRows.Add((
                    pageReader.GetInt64OrDefault("investor_key"),
                    pageReader.GetStringOrEmpty("investor_name"),
                    pageReader.GetStringOrEmpty("investor_type_name")
                ));
            }
        }

        if (pageRows.Count == 0)
        {
            return new PagedResult<InvestorListItemDto>
            {
                Items = [],
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount
            };
        }

        // Aggregate committed totals only for current page investors.
        var aggregateSql = new StringBuilder();
        aggregateSql.Append(" select ");
        aggregateSql.Append(" fc.investor_key, ");
        aggregateSql.Append(" sum(isnull(fc.committed_amount, 0)) as total_invested ");
        aggregateSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        aggregateSql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fc.fund_key ");
        aggregateSql.Append(" where ");
        WarehouseSql.AppendCurrentFundFilter(aggregateSql, "df");
        aggregateSql.Append(" and fc.investor_key in (");

        var aggregateParameters = new List<string>();
        for (var i = 0; i < pageRows.Count; i++)
        {
            aggregateParameters.Add($"@investorKey{i}");
        }

        aggregateSql.Append(string.Join(", ", aggregateParameters));
        aggregateSql.Append(") group by fc.investor_key ");

        var totalsByInvestorKey = new Dictionary<long, decimal>();
        await using (var aggregateCommand = new SqlCommand(aggregateSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        })
        {
            for (var i = 0; i < pageRows.Count; i++)
            {
                aggregateCommand.Parameters.AddWithValue(aggregateParameters[i], pageRows[i].InvestorKey);
            }

            await using var aggregateReader = await aggregateCommand.ExecuteReaderAsync();
            while (await aggregateReader.ReadAsync())
            {
                totalsByInvestorKey[aggregateReader.GetInt64OrDefault("investor_key")] =
                    aggregateReader.GetDecimalOrDefault("total_invested");
            }
        }

        var items = new List<InvestorListItemDto>();
        foreach (var row in pageRows)
        {
            totalsByInvestorKey.TryGetValue(row.InvestorKey, out var totalInvested);

            items.Add(new InvestorListItemDto
            {
                InvestorKey = row.InvestorKey,
                InvestorName = row.InvestorName,
                InvestorType = row.InvestorType,
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
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var fundsSql = new StringBuilder();
        fundsSql.Append(" select ");
        fundsSql.Append(" df.fund_key, ");
        fundsSql.Append(" isnull(df.fund_name, '') as fund_name, ");
        fundsSql.Append(" isnull(df.fund_type_name, '') as fund_type, ");
        fundsSql.Append(" isnull(df.fund_strategy_name, isnull(df.fund_type_name, '')) as fund_category, ");
        fundsSql.Append(" case ");
        fundsSql.Append(" when df.dissolution_date is not null then 'Dissolved' ");
        fundsSql.Append(" when isnull(df.is_current, 1) = 1 then 'Active' ");
        fundsSql.Append(" else 'Inactive' ");
        fundsSql.Append(" end as fund_status ");
        fundsSql.Append(" from ( ");
        fundsSql.Append($" select distinct fund_key from {WarehouseTables.FactCommitted} where investor_key = @investorKey ");
        fundsSql.Append(" union ");
        fundsSql.Append($" select distinct fund_key from {WarehouseTables.FactInvestment} where investor_key = @investorKey ");
        fundsSql.Append(" ) fk ");
        fundsSql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fk.fund_key ");
        fundsSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(fundsSql, "df");
        fundsSql.Append(" order by df.fund_name ");

        var funds = new List<(int FundKey, string FundName, string FundType, string FundCategory, string Status)>();
        await using (var fundsCommand = new SqlCommand(fundsSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        })
        {
            fundsCommand.Parameters.AddWithValue("@investorKey", investorKey);

            await using var fundsReader = await fundsCommand.ExecuteReaderAsync();
            while (await fundsReader.ReadAsync())
            {
                funds.Add((
                    fundsReader.GetInt32OrDefault("fund_key"),
                    fundsReader.GetStringOrEmpty("fund_name"),
                    fundsReader.GetStringOrEmpty("fund_type"),
                    fundsReader.GetStringOrEmpty("fund_category"),
                    fundsReader.GetStringOrEmpty("fund_status")
                ));
            }
        }

        if (funds.Count == 0)
        {
            return [];
        }

        var aggregateSql = new StringBuilder();
        aggregateSql.Append(" select ");
        aggregateSql.Append(" x.fund_key, ");
        aggregateSql.Append(" isnull(comm.invested_amount_total, 0) as invested_amount_total, ");
        aggregateSql.Append(" isnull(inv.invested_amount_fmv_total, 0) as invested_amount_fmv_total, ");
        aggregateSql.Append(" inv.total_return_percent as total_return_percent ");
        aggregateSql.Append(" from ( ");
        aggregateSql.Append($" select distinct fund_key from {WarehouseTables.FactCommitted} where investor_key = @investorKey ");
        aggregateSql.Append(" union ");
        aggregateSql.Append($" select distinct fund_key from {WarehouseTables.FactInvestment} where investor_key = @investorKey ");
        aggregateSql.Append(" ) x ");
        aggregateSql.Append(" left join ( ");
        aggregateSql.Append(" select fc.fund_key, sum(isnull(fc.committed_amount, 0)) as invested_amount_total ");
        aggregateSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        aggregateSql.Append(" where fc.investor_key = @investorKey ");
        aggregateSql.Append(" group by fc.fund_key ");
        aggregateSql.Append(" ) comm on comm.fund_key = x.fund_key ");
        aggregateSql.Append(" left join ( ");
        aggregateSql.Append(" select ");
        aggregateSql.Append(" fi.fund_key, ");
        aggregateSql.Append(" ( ");
        aggregateSql.Append(" sum(CASE WHEN lower(isnull(df.fund_type_name, '')) = 'unitized' ");
        aggregateSql.Append("     THEN isnull(fi.invested_units, 0) ELSE 0 END) ");
        aggregateSql.Append(" + ");
        aggregateSql.Append(" sum(CASE WHEN lower(isnull(df.fund_type_name, '')) <> 'unitized' ");
        aggregateSql.Append("     THEN isnull(fi.invested_amount, 0) ELSE 0 END) ");
        aggregateSql.Append(" ) as invested_amount_fmv_total, ");
        aggregateSql.Append(" case ");
        aggregateSql.Append(" when abs(sum(isnull(fi.invested_amount, 0))) > 0 ");
        aggregateSql.Append(" then ( (sum(isnull(fi.invested_amount_fmv, 0)) - sum(isnull(fi.invested_amount, 0))) ");
        aggregateSql.Append("      / abs(sum(isnull(fi.invested_amount, 0))) ) * 100.0 ");
        aggregateSql.Append(" else null ");
        aggregateSql.Append(" end as total_return_percent ");
        aggregateSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        aggregateSql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fi.fund_key ");
        aggregateSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(aggregateSql, "df");
        aggregateSql.Append(" where fi.investor_key = @investorKey ");
        aggregateSql.Append(" group by fi.fund_key ");
        aggregateSql.Append(" ) inv on inv.fund_key = x.fund_key ");

        var totalsByFundKey = new Dictionary<int, (decimal InvestedAmountTotal, decimal InvestedAmountFmvTotal, decimal? TotalReturnPercent)>();
        await using (var aggregateCommand = new SqlCommand(aggregateSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        })
        {
            aggregateCommand.Parameters.AddWithValue("@investorKey", investorKey);

            await using var aggregateReader = await aggregateCommand.ExecuteReaderAsync();
            while (await aggregateReader.ReadAsync())
            {
                var fundKey = aggregateReader.GetInt32OrDefault("fund_key");
                totalsByFundKey[fundKey] = (
                    aggregateReader.GetDecimalOrDefault("invested_amount_total"),
                    aggregateReader.GetDecimalOrDefault("invested_amount_fmv_total"),
                    aggregateReader.GetNullableDecimal("total_return_percent")
                );
            }
        }

        var items = new List<InvestorInvestmentDto>();
        foreach (var fund in funds)
        {
            totalsByFundKey.TryGetValue(fund.FundKey, out var totals);

            items.Add(new InvestorInvestmentDto
            {
                FundKey = fund.FundKey,
                FundName = fund.FundName,
                FundType = fund.FundType,
                FundCategory = fund.FundCategory,
                Status = fund.Status,
                InvestedAmount = totals.InvestedAmountTotal,
                InvestedAmountFmv = totals.InvestedAmountFmvTotal,
                TotalReturnPercent = totals.TotalReturnPercent
            });
        }

        return items;
    }
}
