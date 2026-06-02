using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public interface IFundPortalService
{
    Task<PagedResult<FundListItemDto>> GetFundsAsync(string? search, int page, int pageSize);
    Task<FundDetailDto?> GetFundByKeyAsync(int fundKey);
    Task<IReadOnlyList<FundInvestorDto>> GetFundInvestorsAsync(int fundKey, string? search);
    Task<IReadOnlyList<FundNavDto>> GetFundNavAsync(int fundKey);
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

    public async Task<IReadOnlyList<FundInvestorDto>> GetFundInvestorsAsync(int fundKey, string? search)
    {
        try
        {
            return await GetFundInvestorsInternalAsync(fundKey, search);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investors for fund {FundKey} cancelled", fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investors for fund {FundKey}. Search={Search}", fundKey, search);
            throw;
        }
    }

    public async Task<IReadOnlyList<FundNavDto>> GetFundNavAsync(int fundKey)
    {
        try
        {
            return await GetFundNavInternalAsync(fundKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get NAV for fund {FundKey} cancelled", fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving NAV for fund {FundKey}", fundKey);
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

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" f.fund_key, ");
        pageSql.Append(" f.fund_name, ");
        pageSql.Append(" isnull(f.fund_strategy_name, isnull(f.fund_type_name, '')) as category, ");
        pageSql.Append(" lower(isnull(f.fund_type_name, '')) as fund_type_name_lower ");
        pageSql.Append($" from {WarehouseTables.DimFund} f ");
        pageSql.Append(" where ");
        WarehouseSql.AppendCurrentFundFilter(pageSql, "f");
        WarehouseSql.AppendFundSearchFilter(pageSql, "f");
        pageSql.Append(" order by f.fund_name ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        pageCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var pageRows = new List<(int FundKey, string FundName, string Category, string FundTypeNameLower)>();
        await using (var pageReader = await pageCommand.ExecuteReaderAsync())
        {
            while (await pageReader.ReadAsync())
            {
                pageRows.Add(
                    (
                        pageReader.GetInt32OrDefault("fund_key"),
                        pageReader.GetStringOrEmpty("fund_name"),
                        pageReader.GetStringOrEmpty("category"),
                        pageReader.GetStringOrEmpty("fund_type_name_lower")
                    )
                );
            }
        }

        if (pageRows.Count == 0)
        {
            return new PagedResult<FundListItemDto>
            {
                Items = [],
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount
            };
        }

        var aggregateSql = new StringBuilder();
        aggregateSql.Append(" select ");
        aggregateSql.Append(" fi.fund_key, ");
        aggregateSql.Append(" sum(isnull(fi.invested_amount, 0)) as invested_amount_total, ");
        aggregateSql.Append(" sum(isnull(fi.invested_amount_fmv, 0)) as invested_amount_fmv_total, ");
        aggregateSql.Append(" sum(isnull(fi.invested_units, 0)) as invested_units_total ");
        aggregateSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        aggregateSql.Append(" where fi.fund_key in (");

        var aggregateParameters = new List<string>();
        for (var i = 0; i < pageRows.Count; i++)
        {
            aggregateParameters.Add($"@fundKey{i}");
        }

        aggregateSql.Append(string.Join(", ", aggregateParameters));
        aggregateSql.Append(") group by fi.fund_key ");

        var aggregateByFundKey = new Dictionary<int, (decimal InvestedAmountTotal, decimal InvestedAmountFmvTotal, decimal InvestedUnitsTotal)>();
        await using (var aggregateCommand = new SqlCommand(aggregateSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        })
        {
            for (var i = 0; i < pageRows.Count; i++)
            {
                aggregateCommand.Parameters.AddWithValue(aggregateParameters[i], pageRows[i].FundKey);
            }

            await using var aggregateReader = await aggregateCommand.ExecuteReaderAsync();
            while (await aggregateReader.ReadAsync())
            {
                var fundKey = aggregateReader.GetInt32OrDefault("fund_key");
                aggregateByFundKey[fundKey] =
                (
                    aggregateReader.GetDecimalOrDefault("invested_amount_total"),
                    aggregateReader.GetDecimalOrDefault("invested_amount_fmv_total"),
                    aggregateReader.GetDecimalOrDefault("invested_units_total")
                );
            }
        }

        var items = new List<FundListItemDto>();
        foreach (var row in pageRows)
        {
            aggregateByFundKey.TryGetValue(
                row.FundKey,
                out var aggregateTotals
            );

            var currentValue = row.FundTypeNameLower == "unitized"
                ? aggregateTotals.InvestedUnitsTotal
                : aggregateTotals.InvestedAmountTotal;

            items.Add(new FundListItemDto
            {
                FundKey = row.FundKey,
                FundName = row.FundName,
                Category = row.Category,
                CurrentValue = currentValue
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
        var summarySql = new StringBuilder();
        summarySql.Append(" select ");
        summarySql.Append(" f.fund_key, ");
        summarySql.Append(" f.fund_id, ");
        summarySql.Append(" isnull(f.fund_code, '') as fund_code, ");
        summarySql.Append(" f.fund_name, ");
        summarySql.Append(" isnull(f.fund_type_name, 'Fund') as fund_type_name, ");
        summarySql.Append(" case when isnull(f.is_active, 0) = 1 then 'Active' else 'Inactive' end as fund_status, ");
        summarySql.Append(" isnull(port.commitment, 0) as commitment, ");
        summarySql.Append(" isnull(port.called, 0) as called, ");
        summarySql.Append(" isnull(port.netinvestedamount, 0) as netinvestedamount, ");
        summarySql.Append(" isnull(port.netinvestedunits, 0) as netinvestedunits, ");
        summarySql.Append(" isnull(port.reserveamount, 0) as reserveamount, ");
        summarySql.Append(" isnull(assets.assets_count, 0) as assets_count, ");
        summarySql.Append(" isnull(inv.investors_count, 0) as investors_count ");
        summarySql.Append($" from {WarehouseTables.DimFund} f ");
        summarySql.Append(" outer apply ( ");
        summarySql.Append(" select ");
        summarySql.Append(" commitment = sum(commitment_amount), ");
        summarySql.Append(" called = sum(capital_called_amount), ");
        summarySql.Append(" netinvestedamount = sum(net_invested_capital_amount), ");
        summarySql.Append(" netinvestedunits = sum(net_invested_capital_units), ");
        summarySql.Append(" reserveamount = sum(reserved_amount) ");
        summarySql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} ");
        summarySql.Append(" where fund_key = f.fund_key ");
        summarySql.Append(" ) port ");
        summarySql.Append(" outer apply ( ");
        summarySql.Append(" select count(*) as assets_count ");
        summarySql.Append($" from {WarehouseTables.DimProperty} p ");
        summarySql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(summarySql, "p");
        WarehouseSql.AppendPropertyBelongsToFundFilter(summarySql, "p", "f");
        summarySql.Append(" ) assets ");
        summarySql.Append(" outer apply ( ");
        summarySql.Append(" select count(*) as investors_count ");
        summarySql.Append(" from ( ");
        summarySql.Append($" select distinct investor_key from {WarehouseTables.FactCommitted} where fund_key = f.fund_key ");
        summarySql.Append(" union ");
        summarySql.Append($" select distinct investor_key from {WarehouseTables.FactInvestment} where fund_key = f.fund_key ");
        summarySql.Append(" ) invkeys ");
        summarySql.Append(" ) inv ");
        summarySql.Append(" where f.fund_key = @fundKey ");
        summarySql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(summarySql, "f");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var summaryCommand = new SqlCommand(summarySql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        summaryCommand.Parameters.AddWithValue("@fundKey", fundKey);

        FundSummaryDto summary;
        await using (var summaryReader = await summaryCommand.ExecuteReaderAsync())
        {
            if (!await summaryReader.ReadAsync())
            {
                return null;
            }

            summary = new FundSummaryDto
            {
                FundId = summaryReader.GetInt32OrDefault("fund_id"),
                FundCode = summaryReader.GetStringOrEmpty("fund_code"),
                FundName = summaryReader.GetStringOrEmpty("fund_name"),
                FundType = summaryReader.GetStringOrEmpty("fund_type_name"),
                Status = summaryReader.GetStringOrEmpty("fund_status"),
                Assets = summaryReader.GetInt32OrDefault("assets_count"),
                Investors = summaryReader.GetInt32OrDefault("investors_count"),
                FundKey = summaryReader.GetInt32OrDefault("fund_key"),
                Commitment = summaryReader.GetDecimalOrDefault("commitment"),
                Called = summaryReader.GetDecimalOrDefault("called"),
                Netinvestedamount = summaryReader.GetDecimalOrDefault("netinvestedamount"),
                Netinvestedunits = summaryReader.GetDecimalOrDefault("netinvestedunits"),
                Reserveamount = summaryReader.GetDecimalOrDefault("reserveamount")
            };
        }
        var financialSummary = new List<DynamicFieldDto>
        {
            DisplayFieldBuilder.ToDynamicField("commitment", DisplayFieldBuilder.Money(summary.Commitment)),
            DisplayFieldBuilder.ToDynamicField("called", DisplayFieldBuilder.Money(summary.Called)),
            DisplayFieldBuilder.ToDynamicField("netInvestedAmount", DisplayFieldBuilder.Money(summary.Netinvestedamount)),
            DisplayFieldBuilder.ToDynamicField("netInvestedUnits", DisplayFieldBuilder.Money(summary.Netinvestedunits)),
            DisplayFieldBuilder.ToDynamicField("reserveAmount", DisplayFieldBuilder.Money(summary.Reserveamount))
        };

        var sectionSql = new StringBuilder();
        sectionSql.Append(" select ");
        sectionSql.Append(" isnull(f.fund_type_name, 'Fund') as fund_type_name, ");
        sectionSql.Append(" isnull(f.fund_strategy_name, '') as fund_strategy_name, ");
        sectionSql.Append(" case when isnull(f.is_active, 0) = 1 then 'Active' else 'Inactive' end as fund_status, ");
        sectionSql.Append(" f.fund_start_date, ");
        sectionSql.Append(" isnull(f.is_sidecar, 0) as is_sidecar ");
        sectionSql.Append($" from {WarehouseTables.DimFund} f ");
        sectionSql.Append(" where f.fund_key = @fundKey ");
        sectionSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sectionSql, "f");

        await using var sectionCommand = new SqlCommand(sectionSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        sectionCommand.Parameters.AddWithValue("@fundKey", fundKey);

        await using var sectionReader = await sectionCommand.ExecuteReaderAsync();

        var investmentDetails = new List<DynamicFieldDto>();

        if (await sectionReader.ReadAsync())
        {
            var fundType = sectionReader.GetStringOrEmpty("fund_type_name");
            var fundStrategy = sectionReader.GetStringOrEmpty("fund_strategy_name");
            var status = sectionReader.GetStringOrEmpty("fund_status");
            var fundStartDate = sectionReader.GetNullableDateTime("fund_start_date");
            var isSidecar = sectionReader.GetInt32OrDefault("is_sidecar") == 1;

            investmentDetails =
            [
                DisplayFieldBuilder.ToDynamicField("investmentType", DisplayFieldBuilder.Text(fundType)),
                DisplayFieldBuilder.ToDynamicField("strategy", DisplayFieldBuilder.Text(fundStrategy)),
                DisplayFieldBuilder.ToDynamicField("startDate", DisplayFieldBuilder.Date(fundStartDate)),
                DisplayFieldBuilder.ToDynamicField("status", DisplayFieldBuilder.Status(status))
            ];
            financialSummary.Add(DisplayFieldBuilder.ToDynamicField("isSidecar", DisplayFieldBuilder.Boolean(isSidecar)));
        }

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

    private async Task<IReadOnlyList<FundInvestorDto>> GetFundInvestorsInternalAsync(int fundKey, string? search)
    {
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var investorsSql = new StringBuilder();
        investorsSql.Append(" select ");
        investorsSql.Append(" i.investor_key, ");
        investorsSql.Append(" i.investor_name, ");
        investorsSql.Append(" isnull(i.investor_type_name, '') as investor_type_name, ");
        investorsSql.Append(" case when isnull(i.is_current, 1) = 1 then 'Active' else 'Inactive' end as investor_status, ");
        investorsSql.Append(" i.valid_from as member_since, ");
        investorsSql.Append(" year(i.valid_from) as join_year ");
        investorsSql.Append(" from ( ");
        investorsSql.Append($" select distinct investor_key from {WarehouseTables.FactCommitted} where fund_key = @fundKey ");
        investorsSql.Append(" union ");
        investorsSql.Append($" select distinct investor_key from {WarehouseTables.FactInvestment} where fund_key = @fundKey ");
        investorsSql.Append(" ) x ");
        investorsSql.Append($" inner join {WarehouseTables.DimInvestor} i on i.investor_key = x.investor_key ");
        investorsSql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(investorsSql, "i");
        WarehouseSql.AppendInvestorSearchFilter(investorsSql, "i");
        investorsSql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = @fundKey ");
        investorsSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(investorsSql, "df");
        investorsSql.Append(" order by i.investor_name ");

        await using var investorsCommand = new SqlCommand(investorsSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        investorsCommand.Parameters.AddWithValue("@fundKey", fundKey);
        investorsCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);

        var investors = new List<(long InvestorKey, string InvestorName, string InvestorType, string Status, DateTime? MemberSince, int? JoinYear)>();
        await using (var investorsReader = await investorsCommand.ExecuteReaderAsync())
        {
            while (await investorsReader.ReadAsync())
            {
                var memberSince = investorsReader.GetNullableDateTime("member_since");
                var joinYearOrdinal = investorsReader.GetOrdinal("join_year");
                int? joinYear = investorsReader.IsDBNull(joinYearOrdinal) ? null : Convert.ToInt32(investorsReader.GetValue(joinYearOrdinal));

                investors.Add((
                    investorsReader.GetInt64OrDefault("investor_key"),
                    investorsReader.GetStringOrEmpty("investor_name"),
                    investorsReader.GetStringOrEmpty("investor_type_name"),
                    investorsReader.GetStringOrEmpty("investor_status"),
                    memberSince,
                    joinYear
                ));
            }
        }

        if (investors.Count == 0)
        {
            return [];
        }

        var aggregateSql = new StringBuilder();
        aggregateSql.Append(" select ");
        aggregateSql.Append(" x.investor_key, ");
        aggregateSql.Append(" isnull(comm.total_invested_amount, 0) as total_invested_amount, ");
        aggregateSql.Append(" isnull(inv.total_invested_fmv, 0) as total_invested_fmv ");
        aggregateSql.Append(" from ( ");
        aggregateSql.Append($" select distinct investor_key from {WarehouseTables.FactCommitted} where fund_key = @fundKey ");
        aggregateSql.Append(" union ");
        aggregateSql.Append($" select distinct investor_key from {WarehouseTables.FactInvestment} where fund_key = @fundKey ");
        aggregateSql.Append(" ) x ");
        aggregateSql.Append(" left join ( ");
        aggregateSql.Append(" select fc.investor_key, sum(isnull(fc.committed_amount, 0)) as total_invested_amount ");
        aggregateSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        aggregateSql.Append(" where fc.fund_key = @fundKey ");
        aggregateSql.Append(" group by fc.investor_key ");
        aggregateSql.Append(" ) comm on comm.investor_key = x.investor_key ");
        aggregateSql.Append(" left join ( ");
        aggregateSql.Append(" select ");
        aggregateSql.Append(" fi.investor_key, ");
        aggregateSql.Append(" ( ");
        aggregateSql.Append(" sum(CASE WHEN lower(isnull(df.fund_type_name, '')) = 'unitized' ");
        aggregateSql.Append("     THEN isnull(fi.invested_units, 0) ELSE 0 END) ");
        aggregateSql.Append(" + ");
        aggregateSql.Append(" sum(CASE WHEN lower(isnull(df.fund_type_name, '')) <> 'unitized' ");
        aggregateSql.Append("     THEN isnull(fi.invested_amount, 0) ELSE 0 END) ");
        aggregateSql.Append(" ) as total_invested_fmv ");
        aggregateSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        aggregateSql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = @fundKey ");
        aggregateSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(aggregateSql, "df");
        aggregateSql.Append(" where fi.fund_key = @fundKey ");
        aggregateSql.Append(" group by fi.investor_key ");
        aggregateSql.Append(" ) inv on inv.investor_key = x.investor_key ");

        var totalsByInvestorKey = new Dictionary<long, (decimal TotalInvestedAmount, decimal TotalInvestedFmv)>();
        await using (var aggregateCommand = new SqlCommand(aggregateSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        })
        {
            aggregateCommand.Parameters.AddWithValue("@fundKey", fundKey);

            await using var aggregateReader = await aggregateCommand.ExecuteReaderAsync();
            while (await aggregateReader.ReadAsync())
            {
                var investorKey = aggregateReader.GetInt64OrDefault("investor_key");
                totalsByInvestorKey[investorKey] = (
                    aggregateReader.GetDecimalOrDefault("total_invested_amount"),
                    aggregateReader.GetDecimalOrDefault("total_invested_fmv")
                );
            }
        }

        var items = new List<FundInvestorDto>();
        foreach (var investor in investors)
        {
            totalsByInvestorKey.TryGetValue(investor.InvestorKey, out var totals);

            items.Add(new FundInvestorDto
            {
                InvestorKey = investor.InvestorKey,
                InvestorName = investor.InvestorName,
                InvestorType = investor.InvestorType,
                Status = investor.Status,
                TotalInvested = totals.TotalInvestedAmount,
                TotalInvestedFmv = totals.TotalInvestedFmv,
                MemberSince = investor.MemberSince,
                JoinYear = investor.JoinYear
            });
        }

        return items;
    }

    private async Task<IReadOnlyList<FundNavDto>> GetFundNavInternalAsync(int fundKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" a.fund_key, ");
        sql.Append(" b.fund_name, ");
        sql.Append(" a.date_key, ");
        sql.Append(" a.nav ");
        sql.Append($" from {WarehouseTables.FactFundNav} a ");
        sql.Append($" inner join {WarehouseTables.DimFund} b on a.fund_key = b.fund_key ");
        sql.Append(" where a.fund_key = @fundKey ");
        sql.Append(" order by a.date_key ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@fundKey", fundKey);

        var items = new List<FundNavDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new FundNavDto
            {
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                DateKey = reader.GetInt32OrDefault("date_key"),
                Nav = reader.GetDecimalOrDefault("nav")
            });
        }

        return items;
    }
}
