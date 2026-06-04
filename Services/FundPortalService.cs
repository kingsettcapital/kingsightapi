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
    Task<PagedResult<FundPeriodDto>> GetFundPeriodsAsync(
        int fundKey,
        TimeGranularity view,
        FundMetricSource source,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetFundCommitmentsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetFundNavAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetFundUnfundedCommitmentsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetFundInvestmentsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
    Task<PagedResult<FundGranularRowDto>> GetFundDistributionsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize);
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

    public async Task<PagedResult<FundPeriodDto>> GetFundPeriodsAsync(
        int fundKey,
        TimeGranularity view,
        FundMetricSource source,
        int page,
        int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => CreateLtdAllPeriodsPage(page, pageSize),
                TimeGranularity.Quarterly => await GetFundPeriodsQuarterlyInternalAsync(fundKey, source, page, pageSize),
                TimeGranularity.Daily => await GetFundPeriodsDailyInternalAsync(fundKey, source, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} periods for fund {FundKey} cancelled", view, fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving {View} periods for fund {FundKey}. Source={Source}, Page={Page}, PageSize={PageSize}",
                view,
                fundKey,
                source,
                page,
                pageSize);
            throw;
        }
    }

    public async Task<PagedResult<FundGranularRowDto>> GetFundNavAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => await GetFundNavLtdInternalAsync(fundKey, page, pageSize),
                TimeGranularity.Quarterly => await GetFundNavQuarterlyInternalAsync(fundKey, period, page, pageSize),
                TimeGranularity.Daily => await GetFundNavDailyInternalAsync(fundKey, period, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} NAV for fund {FundKey} cancelled", view, fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving {View} NAV for fund {FundKey}. Page={Page}, PageSize={PageSize}",
                view,
                fundKey,
                page,
                pageSize);
            throw;
        }
    }

    public async Task<PagedResult<FundGranularRowDto>> GetFundCommitmentsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => await GetFundCommitmentsLtdInternalAsync(fundKey, page, pageSize),
                TimeGranularity.Quarterly => await GetFundCommitmentsQuarterlyInternalAsync(fundKey, period, page, pageSize),
                TimeGranularity.Daily => await GetFundCommitmentsDailyInternalAsync(fundKey, period, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} commitments for fund {FundKey} cancelled", view, fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving {View} commitments for fund {FundKey}. Page={Page}, PageSize={PageSize}",
                view,
                fundKey,
                page,
                pageSize);
            throw;
        }
    }

    public async Task<PagedResult<FundGranularRowDto>> GetFundUnfundedCommitmentsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => await GetFundUnfundedCommitmentsLtdInternalAsync(fundKey, page, pageSize),
                TimeGranularity.Quarterly => await GetFundUnfundedCommitmentsQuarterlyInternalAsync(fundKey, period, page, pageSize),
                TimeGranularity.Daily => await GetFundUnfundedCommitmentsDailyInternalAsync(fundKey, period, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} unfunded commitments for fund {FundKey} cancelled", view, fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving {View} unfunded commitments for fund {FundKey}. Page={Page}, PageSize={PageSize}",
                view,
                fundKey,
                page,
                pageSize);
            throw;
        }
    }

    public async Task<PagedResult<FundGranularRowDto>> GetFundInvestmentsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => await GetFundInvestmentsLtdInternalAsync(fundKey, page, pageSize),
                TimeGranularity.Quarterly => await GetFundInvestmentsQuarterlyInternalAsync(fundKey, period, page, pageSize),
                TimeGranularity.Daily => await GetFundInvestmentsDailyInternalAsync(fundKey, period, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} investments for fund {FundKey} cancelled", view, fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving {View} investments for fund {FundKey}. Page={Page}, PageSize={PageSize}",
                view,
                fundKey,
                page,
                pageSize);
            throw;
        }
    }

    public async Task<PagedResult<FundGranularRowDto>> GetFundDistributionsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => await GetFundDistributionsLtdInternalAsync(fundKey, page, pageSize),
                TimeGranularity.Quarterly => await GetFundDistributionsQuarterlyInternalAsync(fundKey, period, page, pageSize),
                TimeGranularity.Daily => await GetFundDistributionsDailyInternalAsync(fundKey, period, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} distributions for fund {FundKey} cancelled", view, fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving {View} distributions for fund {FundKey}. Page={Page}, PageSize={PageSize}",
                view,
                fundKey,
                page,
                pageSize);
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
            DisplayFieldBuilder.ToDynamicField("netInvestedUnits", DisplayFieldBuilder.Number(summary.Netinvestedunits)),
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

    private static PagedResult<FundPeriodDto> CreateLtdAllPeriodsPage(int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, _) = Pagination.Normalize(page, pageSize);
        return new PagedResult<FundPeriodDto>
        {
            Items = normalizedPage == 1
                ?
                [
                    new FundPeriodDto
                    {
                        Label = "All Periods",
                        Disabled = true
                    }
                ]
                : [],
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = 1
        };
    }

    private async Task<PagedResult<FundPeriodDto>> GetFundPeriodsQuarterlyInternalAsync(
        int fundKey,
        FundMetricSource source,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        AppendQuarterlyPeriodSelect(countSql, source);
        countSql.Append(" ) quarterly_periods ");

        var pageSql = new StringBuilder();
        AppendQuarterlyPeriodSelect(pageSql, source);
        pageSql.Append(" order by calendar_year, quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundPeriodPageQueryAsync(fundKey, normalizedPage, normalizedPageSize, offset, countSql, pageSql);
    }

    private async Task<PagedResult<FundPeriodDto>> GetFundPeriodsDailyInternalAsync(
        int fundKey,
        FundMetricSource source,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        AppendDailyPeriodSelect(countSql, source);
        countSql.Append(" ) daily_periods ");

        var pageSql = new StringBuilder();
        AppendDailyPeriodSelect(pageSql, source);
        pageSql.Append(source switch
        {
            FundMetricSource.Commitments or FundMetricSource.UnfundedCommitments => " order by fc.posted_date_key desc ",
            FundMetricSource.Investments => " order by fi.calculation_date_key desc ",
            FundMetricSource.Distributions => " order by fd.calculation_date_key desc ",
            _ => " order by date_key desc "
        });
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundPeriodPageQueryAsync(fundKey, normalizedPage, normalizedPageSize, offset, countSql, pageSql);
    }

    private void AppendQuarterlyPeriodSelect(StringBuilder sql, FundMetricSource source)
    {
        sql.Append(" select ");
        sql.Append(" d.quarter_year, ");
        sql.Append(" d.calendar_year, ");
        sql.Append(" min(d.date_key) as min_date_key, ");
        sql.Append(" max(d.date_key) as max_date_key, ");
        sql.Append(" min(d.first_date_of_quater) as period_start, ");
        sql.Append(" max(d.last_date_of_quater) as period_end, ");
        sql.Append(" max(d.month_year) as month_year ");

        if (source is FundMetricSource.Commitments
            or FundMetricSource.UnfundedCommitments
            or FundMetricSource.Investments)
        {
            sql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} q ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.quarter_year = q.quarter_year ");
            sql.Append(" where q.fund_key = @fundKey ");
            sql.Append(" group by d.quarter_year, d.calendar_year ");
        }
        else if (source is FundMetricSource.Distributions)
        {
            sql.Append($" from {WarehouseTables.FactDistribution} fd ");
            AppendDimFundJoinOnFundKey(sql, "fd");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.calculation_date_key ");
            sql.Append(" where fd.fund_key = @fundKey ");
            sql.Append(" group by d.quarter_year, d.calendar_year ");
            AppendDistributionMetricHaving(sql, "fd");
        }
        else
        {
            sql.Append($" from {WarehouseTables.FactFundNav} n ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = n.date_key ");
            sql.Append(" where n.fund_key = @fundKey ");
            sql.Append(" and isnull(n.nav, 0) != 0 ");
            sql.Append(" group by d.quarter_year, d.calendar_year ");
        }
    }

    private void AppendDailyPeriodSelect(StringBuilder sql, FundMetricSource source)
    {
        sql.Append(" select ");
        if (source is FundMetricSource.Commitments or FundMetricSource.UnfundedCommitments)
        {
            sql.Append(" fc.posted_date_key as date_key, ");
            sql.Append(" d.full_date, ");
        }
        else if (source is FundMetricSource.Investments)
        {
            sql.Append(" fi.calculation_date_key as date_key, ");
            sql.Append(" d.full_date, ");
        }
        else if (source is FundMetricSource.Distributions)
        {
            sql.Append(" fd.calculation_date_key as date_key, ");
            sql.Append(" d.full_date, ");
        }
        else
        {
            sql.Append(" d.date_key, ");
            sql.Append(" d.full_date, ");
        }

        sql.Append(" d.quarter_year, ");
        sql.Append(" d.calendar_year, ");
        sql.Append(" d.month_year, ");
        sql.Append(" d.first_date_of_quater as period_start, ");
        sql.Append(" d.last_date_of_quater as period_end ");

        if (source == FundMetricSource.Commitments)
        {
            sql.Append($" from {WarehouseTables.FactCommitted} fc ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fc.posted_date_key ");
            sql.Append(" where fc.fund_key = @fundKey ");
            sql.Append(" group by ");
            sql.Append(" fc.posted_date_key, d.full_date, d.quarter_year, d.calendar_year, d.month_year, ");
            sql.Append(" d.first_date_of_quater, d.last_date_of_quater ");
            sql.Append(" having sum(fc.committed_amount) != 0 ");
        }
        else if (source == FundMetricSource.UnfundedCommitments)
        {
            sql.Append($" from {WarehouseTables.FactCommitted} fc ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fc.posted_date_key ");
            sql.Append(" where fc.fund_key = @fundKey ");
            sql.Append(" group by ");
            sql.Append(" fc.posted_date_key, d.full_date, d.quarter_year, d.calendar_year, d.month_year, ");
            sql.Append(" d.first_date_of_quater, d.last_date_of_quater ");
            sql.Append(" having sum(fc.committed_amount_fmv) != 0 ");
        }
        else if (source == FundMetricSource.Investments)
        {
            sql.Append($" from {WarehouseTables.FactInvestment} fi ");
            AppendDimFundJoinOnFundKey(sql, "fi");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fi.calculation_date_key ");
            sql.Append(" where fi.fund_key = @fundKey ");
            sql.Append(" group by ");
            sql.Append(" fi.calculation_date_key, d.full_date, d.quarter_year, d.calendar_year, d.month_year, ");
            sql.Append(" d.first_date_of_quater, d.last_date_of_quater ");
            AppendInvestmentMetricHaving(sql, "fi");
        }
        else if (source == FundMetricSource.Distributions)
        {
            sql.Append($" from {WarehouseTables.FactDistribution} fd ");
            AppendDimFundJoinOnFundKey(sql, "fd");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.calculation_date_key ");
            sql.Append(" where fd.fund_key = @fundKey ");
            sql.Append(" group by ");
            sql.Append(" fd.calculation_date_key, d.full_date, d.quarter_year, d.calendar_year, d.month_year, ");
            sql.Append(" d.first_date_of_quater, d.last_date_of_quater ");
            AppendDistributionMetricHaving(sql, "fd");
        }
        else
        {
            sql.Append($" from {WarehouseTables.FactFundNav} n ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = n.date_key ");
            sql.Append(" where n.fund_key = @fundKey ");
            sql.Append(" and isnull(n.nav, 0) != 0 ");
        }
    }

    private async Task<PagedResult<FundPeriodDto>> ExecuteFundPeriodPageQueryAsync(
        int fundKey,
        int normalizedPage,
        int normalizedPageSize,
        int offset,
        StringBuilder countSql,
        StringBuilder pageSql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        countCommand.Parameters.AddWithValue("@fundKey", fundKey);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        pageCommand.Parameters.AddWithValue("@fundKey", fundKey);
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<FundPeriodDto>();
        await using (var reader = await pageCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                items.Add(MapFundPeriod(reader));
            }
        }

        return new PagedResult<FundPeriodDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private static FundPeriodDto MapFundPeriod(SqlDataReader reader)
    {
        var quarterYear = reader.GetNullableStringIfPresent("quarter_year");
        var fullDate = reader.GetNullableDateTimeIfPresent("full_date");
        var dateKeyOrdinal = reader.TryGetOrdinal("date_key", out var dateKeyIndex) && !reader.IsDBNull(dateKeyIndex)
            ? Convert.ToInt32(reader.GetValue(dateKeyIndex))
            : (int?)null;

        var minDateKey = reader.TryGetOrdinal("min_date_key", out var minKeyIndex) && !reader.IsDBNull(minKeyIndex)
            ? Convert.ToInt32(reader.GetValue(minKeyIndex))
            : dateKeyOrdinal;

        string label;
        if (fullDate.HasValue)
        {
            label = fullDate.Value.ToString("d");
        }
        else if (!string.IsNullOrEmpty(quarterYear))
        {
            label = quarterYear;
        }
        else
        {
            label = string.Empty;
        }

        return new FundPeriodDto
        {
            DateKey = dateKeyOrdinal ?? minDateKey,
            FullDate = fullDate,
            Label = label,
            QuarterYear = quarterYear,
            CalendarYear = reader.GetInt32OrDefaultIfPresent("calendar_year"),
            MonthYear = reader.GetNullableStringIfPresent("month_year"),
            PeriodStart = reader.GetNullableDateTimeIfPresent("period_start"),
            PeriodEnd = reader.GetNullableDateTimeIfPresent("period_end")
        };
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundCommitmentsLtdInternalAsync(int fundKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select case when exists ( ");
        countSql.Append(" select 1 ");
        countSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} ");
        countSql.Append(" where fund_key = @fundKey ");
        countSql.Append(" ) then 1 else 0 end ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" Period = 'Life To Date', ");
        pageSql.Append(" commitment_amount = sum(commitment_amount), ");
        pageSql.Append(" Description = 'Total Commitment as of Date' ");
        pageSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} ");
        pageSql.Append(" where fund_key = @fundKey ");
        pageSql.Append(" order by Period ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => new FundGranularRowDto
            {
                Period = reader.GetStringOrEmpty("Period"),
                Amount = reader.GetDecimalOrDefault("commitment_amount"),
                Units = 0,
                Description = reader.GetStringOrEmpty("Description")
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundCommitmentsQuarterlyInternalAsync(
        int fundKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select quarter_year ");
        countSql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} ");
        countSql.Append(" where fund_key = @fundKey ");
        AppendPortfolioQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by quarter_year ");
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" Period = quarter_year, ");
        pageSql.Append(" commitment_amount = sum(commitment_amount), ");
        pageSql.Append(" Description = 'Quarterly Commitment' ");
        pageSql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} ");
        pageSql.Append(" where fund_key = @fundKey ");
        AppendPortfolioQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by quarter_year ");
        pageSql.Append(" order by quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => new FundGranularRowDto
            {
                Period = reader.GetStringOrEmpty("Period"),
                Amount = reader.GetDecimalOrDefault("commitment_amount"),
                Units = 0,
                Description = reader.GetStringOrEmpty("Description")
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundCommitmentsDailyInternalAsync(
        int fundKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select fc.fund_key, fc.posted_date_key ");
        countSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        AppendCommitmentDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by fc.fund_key, fc.posted_date_key ");
        countSql.Append(" having sum(fc.committed_amount) != 0 ");
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" fc.fund_key, ");
        pageSql.Append(" fc.posted_date_key, ");
        pageSql.Append(" try_convert(date, cast(fc.posted_date_key as varchar(8)), 112) as full_date, ");
        pageSql.Append(" amount = sum(fc.committed_amount) ");
        pageSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        AppendCommitmentDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by fc.fund_key, fc.posted_date_key ");
        pageSql.Append(" having sum(fc.committed_amount) != 0 ");
        pageSql.Append(" order by fc.posted_date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader =>
            {
                var postedDateKey = reader.GetInt32OrDefault("posted_date_key");
                return new FundGranularRowDto
                {
                    Date = reader.GetNullableDateTime("full_date"),
                    PostedDateKey = postedDateKey == 0 ? null : postedDateKey,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Units = 0,
                    Description = string.Empty
                };
            });
    }

    private static void AppendCommitmentDailyPeriodJoinAndWhere(StringBuilder sql, FundPeriodFilter? period)
    {
        sql.Append(" where fc.fund_key = @fundKey ");
        if (period?.HasDateKey == true)
        {
            sql.Append(" and fc.posted_date_key = @dateKey ");
        }
    }

    /// <summary>Filters portfolio quarterly rows by quarter resolved from period dropdown date_key.</summary>
    private static void AppendPortfolioQuarterlyPeriodFilter(StringBuilder sql, FundPeriodFilter? period)
    {
        if (period?.HasDateKey == true)
        {
            sql.Append(" and quarter_year = ( ");
            sql.Append($" select quarter_year from {WarehouseTables.DimDate} where date_key = @dateKey ");
            sql.Append(" ) ");
        }
    }

    /// <summary>Filters dim_date quarter when facts join on date_key (NAV, distributions).</summary>
    private static void AppendDimDateQuarterlyPeriodFilter(StringBuilder sql, FundPeriodFilter? period, string dateAlias = "d")
    {
        if (period?.HasDateKey == true)
        {
            sql.Append($" and {dateAlias}.quarter_year = ( ");
            sql.Append($" select quarter_year from {WarehouseTables.DimDate} where date_key = @dateKey ");
            sql.Append(" ) ");
        }
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundUnfundedCommitmentsLtdInternalAsync(int fundKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select case when exists ( ");
        countSql.Append(" select 1 ");
        countSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} ");
        countSql.Append(" where fund_key = @fundKey ");
        countSql.Append(" ) then 1 else 0 end ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" Period = 'Life To Date', ");
        pageSql.Append(" amount = sum(unfunded_amount), ");
        pageSql.Append(" Description = 'Total Unfunded Commitment' ");
        pageSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} ");
        pageSql.Append(" where fund_key = @fundKey ");
        pageSql.Append(" order by Period ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => new FundGranularRowDto
            {
                Period = reader.GetStringOrEmpty("Period"),
                Amount = reader.GetDecimalOrDefault("amount"),
                Units = 0,
                Description = reader.GetStringOrEmpty("Description")
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundUnfundedCommitmentsQuarterlyInternalAsync(
        int fundKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select quarter_year ");
        countSql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} ");
        countSql.Append(" where fund_key = @fundKey ");
        AppendPortfolioQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by quarter_year ");
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" Period = quarter_year, ");
        pageSql.Append(" amount = sum(unfunded_amount), ");
        pageSql.Append(" Description = 'Quarterly Unfunded' ");
        pageSql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} ");
        pageSql.Append(" where fund_key = @fundKey ");
        AppendPortfolioQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by quarter_year ");
        pageSql.Append(" order by quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader =>
            {
                var periodLabel = reader.GetStringOrEmpty("Period");
                return new FundGranularRowDto
                {
                    Period = periodLabel,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Units = 0,
                    Description = string.IsNullOrEmpty(periodLabel)
                        ? reader.GetStringOrEmpty("Description")
                        : $"{periodLabel} Unfunded Commitment"
                };
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundUnfundedCommitmentsDailyInternalAsync(
        int fundKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select fc.fund_key, fc.posted_date_key ");
        countSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        AppendCommitmentDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by fc.fund_key, fc.posted_date_key ");
        countSql.Append(" having sum(fc.committed_amount_fmv) != 0 ");
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" fc.fund_key, ");
        pageSql.Append(" fc.posted_date_key, ");
        pageSql.Append(" try_convert(date, cast(fc.posted_date_key as varchar(8)), 112) as full_date, ");
        pageSql.Append(" amount = sum(fc.committed_amount_fmv) ");
        pageSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        AppendCommitmentDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by fc.fund_key, fc.posted_date_key ");
        pageSql.Append(" having sum(fc.committed_amount_fmv) != 0 ");
        pageSql.Append(" order by fc.posted_date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader =>
            {
                var postedDateKey = reader.GetInt32OrDefault("posted_date_key");
                return new FundGranularRowDto
                {
                    Date = reader.GetNullableDateTime("full_date"),
                    PostedDateKey = postedDateKey == 0 ? null : postedDateKey,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Units = 0,
                    Description = "Remaining commitment"
                };
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundNavLtdInternalAsync(int fundKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select case when exists ( ");
        countSql.Append(" select 1 ");
        countSql.Append($" from {WarehouseTables.FactFundNav} ");
        countSql.Append(" where fund_key = @fundKey ");
        countSql.Append(" ) then 1 else 0 end ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" Period = 'Life To Date', ");
        pageSql.Append(" amount = sum(nav), ");
        pageSql.Append(" Description = 'Total NAV' ");
        pageSql.Append($" from {WarehouseTables.FactFundNav} ");
        pageSql.Append(" where fund_key = @fundKey ");
        pageSql.Append(" order by Period ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => new FundGranularRowDto
            {
                Period = reader.GetStringOrEmpty("Period"),
                Amount = reader.GetDecimalOrDefault("amount"),
                Units = reader.GetDecimalFromColumns("units", "nav_units"),
                Description = reader.GetStringOrEmpty("Description")
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundNavQuarterlyInternalAsync(
        int fundKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select d.quarter_year ");
        countSql.Append($" from {WarehouseTables.FactFundNav} n ");
        countSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = n.date_key ");
        countSql.Append(" where n.fund_key = @fundKey ");
        AppendDimDateQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by d.quarter_year ");
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" Period = d.quarter_year, ");
        pageSql.Append(" amount = sum(n.nav), ");
        pageSql.Append(" Description = 'Quarterly NAV' ");
        pageSql.Append($" from {WarehouseTables.FactFundNav} n ");
        pageSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = n.date_key ");
        pageSql.Append(" where n.fund_key = @fundKey ");
        AppendDimDateQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by d.quarter_year ");
        pageSql.Append(" order by d.quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader =>
            {
                var periodLabel = reader.GetStringOrEmpty("Period");
                return new FundGranularRowDto
                {
                    Period = periodLabel,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Units = reader.GetDecimalFromColumns("units", "nav_units"),
                    Description = string.IsNullOrEmpty(periodLabel) ? reader.GetStringOrEmpty("Description") : $"{periodLabel} NAV"
                };
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundNavDailyInternalAsync(
        int fundKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select n.fund_key, n.date_key ");
        countSql.Append($" from {WarehouseTables.FactFundNav} n ");
        AppendNavDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" and isnull(n.nav, 0) != 0 ");
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" n.date_key, ");
        pageSql.Append(" try_convert(date, cast(n.date_key as varchar(8)), 112) as nav_date, ");
        pageSql.Append(" amount = n.nav ");
        pageSql.Append($" from {WarehouseTables.FactFundNav} n ");
        AppendNavDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" and isnull(n.nav, 0) != 0 ");
        pageSql.Append(" order by n.date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader =>
            {
                var dateKey = reader.GetInt32OrDefault("date_key");
                return new FundGranularRowDto
                {
                    Date = reader.GetNullableDateTime("nav_date"),
                    PostedDateKey = dateKey == 0 ? null : dateKey,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Units = reader.GetDecimalFromColumns("units", "nav_units"),
                    Description = string.Empty
                };
            });
    }

    private static void AppendNavDailyPeriodJoinAndWhere(StringBuilder sql, FundPeriodFilter? period)
    {
        sql.Append(" where n.fund_key = @fundKey ");
        if (period?.HasDateKey == true)
        {
            sql.Append(" and n.date_key = @dateKey ");
        }
    }

    private static void AppendDimFundJoinOnFundKey(StringBuilder sql, string factAlias)
    {
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = {factAlias}.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
    }

    private static void AppendInvestmentAmountUnitsSelect(StringBuilder sql, string factAlias)
    {
        sql.Append(" amount = case when lower(isnull(max(df.fund_type_name), '')) = 'unitized' ");
        sql.Append(" then cast(0 as decimal(38, 10)) ");
        sql.Append($" else sum(isnull({factAlias}.invested_amount, 0)) end, ");
        sql.Append(" units = case when lower(isnull(max(df.fund_type_name), '')) = 'unitized' ");
        sql.Append($" then sum(isnull({factAlias}.invested_units, 0)) ");
        sql.Append(" else cast(0 as decimal(38, 10)) end ");
    }

    private static void AppendInvestmentMetricHaving(StringBuilder sql, string factAlias)
    {
        sql.Append(" having sum(case when lower(isnull(df.fund_type_name, '')) = 'unitized' ");
        sql.Append($" then isnull({factAlias}.invested_units, 0) ");
        sql.Append($" else isnull({factAlias}.invested_amount, 0) end) != 0 ");
    }

    private static void AppendDistributionAmountUnitsSelect(StringBuilder sql, string factAlias)
    {
        sql.Append(" amount = case when lower(isnull(max(df.fund_type_name), '')) = 'unitized' ");
        sql.Append(" then cast(0 as decimal(38, 10)) ");
        sql.Append($" else sum(isnull({factAlias}.distributed_amount, 0)) end, ");
        sql.Append(" units = case when lower(isnull(max(df.fund_type_name), '')) = 'unitized' ");
        sql.Append($" then sum(isnull({factAlias}.distributed_units, 0)) ");
        sql.Append(" else cast(0 as decimal(38, 10)) end ");
    }

    private static void AppendDistributionMetricHaving(StringBuilder sql, string factAlias)
    {
        sql.Append(" having sum(case when lower(isnull(df.fund_type_name, '')) = 'unitized' ");
        sql.Append($" then isnull({factAlias}.distributed_units, 0) ");
        sql.Append($" else isnull({factAlias}.distributed_amount, 0) end) != 0 ");
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundInvestmentsLtdInternalAsync(int fundKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select case when exists ( ");
        countSql.Append(" select 1 ");
        countSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} ");
        countSql.Append(" where fund_key = @fundKey ");
        countSql.Append(" ) then 1 else 0 end ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" Period = 'Life To Date', ");
        pageSql.Append(" amount = sum(isnull(net_invested_capital_amount, 0)), ");
        pageSql.Append(" units = sum(isnull(net_invested_capital_units, 0)), ");
        pageSql.Append(" Description = 'Total Investment' ");
        pageSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} ");
        pageSql.Append(" where fund_key = @fundKey ");
        pageSql.Append(" order by Period ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => new FundGranularRowDto
            {
                Period = reader.GetStringOrEmpty("Period"),
                Amount = reader.GetDecimalOrDefault("amount"),
                Units = reader.GetDecimalOrDefault("units"),
                Description = reader.GetStringOrEmpty("Description")
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundInvestmentsQuarterlyInternalAsync(
        int fundKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select quarter_year ");
        countSql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} ");
        countSql.Append(" where fund_key = @fundKey ");
        AppendPortfolioQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by quarter_year ");
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" Period = quarter_year, ");
        pageSql.Append(" amount = sum(isnull(net_invested_capital_amount, 0)), ");
        pageSql.Append(" units = sum(isnull(net_invested_capital_units, 0)), ");
        pageSql.Append(" Description = 'Quarterly Investment' ");
        pageSql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} ");
        pageSql.Append(" where fund_key = @fundKey ");
        AppendPortfolioQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by quarter_year ");
        pageSql.Append(" order by quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader =>
            {
                var periodLabel = reader.GetStringOrEmpty("Period");
                return new FundGranularRowDto
                {
                    Period = periodLabel,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Units = reader.GetDecimalOrDefault("units"),
                    Description = string.IsNullOrEmpty(periodLabel)
                        ? reader.GetStringOrEmpty("Description")
                        : $"{periodLabel} Investment"
                };
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundInvestmentsDailyInternalAsync(
        int fundKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select fi.fund_key, fi.calculation_date_key ");
        countSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        AppendDimFundJoinOnFundKey(countSql, "fi");
        AppendInvestmentDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by fi.fund_key, fi.calculation_date_key ");
        AppendInvestmentMetricHaving(countSql, "fi");
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" fi.calculation_date_key, ");
        pageSql.Append(" try_convert(date, cast(fi.calculation_date_key as varchar(8)), 112) as full_date, ");
        AppendInvestmentAmountUnitsSelect(pageSql, "fi");
        pageSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        AppendDimFundJoinOnFundKey(pageSql, "fi");
        AppendInvestmentDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by fi.fund_key, fi.calculation_date_key ");
        AppendInvestmentMetricHaving(pageSql, "fi");
        pageSql.Append(" order by fi.calculation_date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader =>
            {
                var calculationDateKey = reader.GetInt32OrDefault("calculation_date_key");
                return new FundGranularRowDto
                {
                    Date = reader.GetNullableDateTime("full_date"),
                    PostedDateKey = calculationDateKey == 0 ? null : calculationDateKey,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Units = reader.GetDecimalOrDefault("units"),
                    Description = string.Empty
                };
            });
    }

    private static void AppendInvestmentDailyPeriodJoinAndWhere(StringBuilder sql, FundPeriodFilter? period)
    {
        sql.Append(" where fi.fund_key = @fundKey ");
        if (period?.HasDateKey == true)
        {
            sql.Append(" and fi.calculation_date_key = @dateKey ");
        }
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundDistributionsLtdInternalAsync(int fundKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select case when exists ( ");
        countSql.Append(" select 1 ");
        countSql.Append($" from {WarehouseTables.FactDistribution} ");
        countSql.Append(" where fund_key = @fundKey ");
        countSql.Append(" ) then 1 else 0 end ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" Period = 'Life To Date', ");
        AppendDistributionAmountUnitsSelect(pageSql, "fd");
        pageSql.Append(", Description = 'Total Distribution' ");
        pageSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinOnFundKey(pageSql, "fd");
        pageSql.Append(" where fd.fund_key = @fundKey ");
        pageSql.Append(" order by Period ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => new FundGranularRowDto
            {
                Period = reader.GetStringOrEmpty("Period"),
                Amount = reader.GetDecimalOrDefault("amount"),
                Units = reader.GetDecimalOrDefault("units"),
                Description = reader.GetStringOrEmpty("Description")
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundDistributionsQuarterlyInternalAsync(
        int fundKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select d.quarter_year ");
        countSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinOnFundKey(countSql, "fd");
        countSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.calculation_date_key ");
        countSql.Append(" where fd.fund_key = @fundKey ");
        AppendDimDateQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by d.quarter_year ");
        AppendDistributionMetricHaving(countSql, "fd");
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" Period = d.quarter_year, ");
        AppendDistributionAmountUnitsSelect(pageSql, "fd");
        pageSql.Append(", Description = 'Quarterly Distribution' ");
        pageSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinOnFundKey(pageSql, "fd");
        pageSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.calculation_date_key ");
        pageSql.Append(" where fd.fund_key = @fundKey ");
        AppendDimDateQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by d.quarter_year ");
        pageSql.Append(" order by d.quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader =>
            {
                var periodLabel = reader.GetStringOrEmpty("Period");
                return new FundGranularRowDto
                {
                    Period = periodLabel,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Units = reader.GetDecimalOrDefault("units"),
                    Description = string.IsNullOrEmpty(periodLabel)
                        ? reader.GetStringOrEmpty("Description")
                        : $"{periodLabel} Distribution"
                };
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundDistributionsDailyInternalAsync(
        int fundKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select fd.fund_key, fd.calculation_date_key ");
        countSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinOnFundKey(countSql, "fd");
        AppendDistributionDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by fd.fund_key, fd.calculation_date_key ");
        AppendDistributionMetricHaving(countSql, "fd");
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" fd.calculation_date_key, ");
        pageSql.Append(" try_convert(date, cast(fd.calculation_date_key as varchar(8)), 112) as full_date, ");
        AppendDistributionAmountUnitsSelect(pageSql, "fd");
        pageSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinOnFundKey(pageSql, "fd");
        AppendDistributionDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by fd.fund_key, fd.calculation_date_key ");
        AppendDistributionMetricHaving(pageSql, "fd");
        pageSql.Append(" order by fd.calculation_date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader =>
            {
                var calculationDateKey = reader.GetInt32OrDefault("calculation_date_key");
                return new FundGranularRowDto
                {
                    Date = reader.GetNullableDateTime("full_date"),
                    PostedDateKey = calculationDateKey == 0 ? null : calculationDateKey,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Units = reader.GetDecimalOrDefault("units"),
                    Description = string.Empty
                };
            });
    }

    private static void AppendDistributionDailyPeriodJoinAndWhere(StringBuilder sql, FundPeriodFilter? period)
    {
        sql.Append(" where fd.fund_key = @fundKey ");
        if (period?.HasDateKey == true)
        {
            sql.Append(" and fd.calculation_date_key = @dateKey ");
        }
    }

    private async Task<PagedResult<FundGranularRowDto>> ExecuteFundGranularPageQueryAsync(
        int fundKey,
        FundPeriodFilter? period,
        int normalizedPage,
        int normalizedPageSize,
        int offset,
        StringBuilder countSql,
        StringBuilder pageSql,
        Func<SqlDataReader, FundGranularRowDto> mapRow)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddGranularPeriodParameters(countCommand, fundKey, period);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddGranularPeriodParameters(pageCommand, fundKey, period);
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<FundGranularRowDto>();
        await using (var reader = await pageCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                items.Add(mapRow(reader));
            }
        }

        return new PagedResult<FundGranularRowDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private static void AddGranularPeriodParameters(SqlCommand command, int fundKey, FundPeriodFilter? period)
    {
        command.Parameters.AddWithValue("@fundKey", fundKey);
        command.Parameters.AddWithValue("@dateKey", (object?)period?.DateKey ?? DBNull.Value);
    }

}
