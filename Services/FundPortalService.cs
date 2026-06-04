using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed class FundPortalService : IFundPortalService
{
    /// <summary>Max flat distribution rows loaded before grouping (fund-level datasets are small).</summary>
    private const int DistributionGroupingFetchCap = 10_000;

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

    public async Task<PagedResult<FundInvestorDto>> GetFundInvestorsAsync(int fundKey, string? search, int page, int pageSize)
    {
        try
        {
            return await GetFundInvestorsInternalAsync(fundKey, search, page, pageSize);
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

    public async Task<PagedResult<FundDistributionGroupDto>> GetFundDistributionsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        try
        {
            var flat = view switch
            {
                TimeGranularity.Ltd => await GetFundDistributionsLtdInternalAsync(fundKey, 1, DistributionGroupingFetchCap),
                TimeGranularity.Quarterly => await GetFundDistributionsQuarterlyInternalAsync(fundKey, period, 1, DistributionGroupingFetchCap),
                TimeGranularity.Daily => await GetFundDistributionsDailyInternalAsync(fundKey, period, 1, DistributionGroupingFetchCap),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };

            if (flat.TotalCount > flat.Items.Count)
            {
                _logger.LogWarning(
                    "Fund {FundKey} distributions ({View}) truncated at {Cap} rows before grouping; total flat rows {Total}.",
                    fundKey,
                    view,
                    DistributionGroupingFetchCap,
                    flat.TotalCount);
            }

            return BuildGroupedDistributionPage(flat.Items, page, pageSize);
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
        WarehouseSql.AppendPropertyFundLevel000Filter(summarySql, "p");
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

    public async Task<PagedResult<FundAssetDto>> GetFundAssetsAsync(int fundKey, int page, int pageSize)
    {
        try
        {
            return await GetFundAssetsInternalAsync(fundKey, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get assets for fund {FundKey} cancelled", fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets for fund {FundKey}", fundKey);
            throw;
        }
    }

    private async Task<PagedResult<FundAssetDto>> GetFundAssetsInternalAsync(int fundKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append($" from {WarehouseTables.DimProperty} p ");
        countSql.Append($" inner join {WarehouseTables.DimFund} f on f.fund_key = @fundKey ");
        countSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(countSql, "f");
        countSql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(countSql, "p");
        WarehouseSql.AppendPropertyBelongsToFundFilter(countSql, "p", "f");
        WarehouseSql.AppendPropertyFundLevel000Filter(countSql, "p");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" p.property_key, ");
        pageSql.Append(" isnull(p.property_name, '') as property_name, ");
        pageSql.Append(" isnull(p.city, '') as city, ");
        pageSql.Append(" isnull(p.province, '') as province, ");
        pageSql.Append(" isnull(p.geography, '') as geography, ");
        pageSql.Append(" isnull(p.asset_type, '') as asset_type, ");
        pageSql.Append(" isnull(p.investment_type, '') as investment_type, ");
        pageSql.Append(" isnull(p.property_status, '') as property_status, ");
        pageSql.Append(" p.property_acquisition, ");
        pageSql.Append(" p.property_disposition ");
        pageSql.Append($" from {WarehouseTables.DimProperty} p ");
        pageSql.Append($" inner join {WarehouseTables.DimFund} f on f.fund_key = @fundKey ");
        pageSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(pageSql, "f");
        pageSql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(pageSql, "p");
        WarehouseSql.AppendPropertyBelongsToFundFilter(pageSql, "p", "f");
        WarehouseSql.AppendPropertyFundLevel000Filter(pageSql, "p");
        pageSql.Append(" order by p.property_name ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

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

        var items = new List<FundAssetDto>();
        await using (var reader = await pageCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                items.Add(new FundAssetDto
                {
                    PropertyKey = reader.GetInt64OrDefault("property_key"),
                    PropertyName = reader.GetStringOrEmpty("property_name"),
                    City = reader.GetStringOrEmpty("city"),
                    Province = reader.GetStringOrEmpty("province"),
                    Geography = reader.GetStringOrEmpty("geography"),
                    AssetType = reader.GetStringOrEmpty("asset_type"),
                    InvestmentType = reader.GetStringOrEmpty("investment_type"),
                    PropertyStatus = reader.GetStringOrEmpty("property_status"),
                    PropertyAcquisition = reader.GetNullableStringIfPresent("property_acquisition"),
                    PropertyDisposition = reader.GetNullableStringIfPresent("property_disposition")
                });
            }
        }

        return new PagedResult<FundAssetDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private async Task<PagedResult<FundInvestorDto>> GetFundInvestorsInternalAsync(int fundKey, string? search, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        AppendFundInvestorsBaseSelect(countSql);
        countSql.Append(" ) investor_rows ");

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        countCommand.Parameters.AddWithValue("@fundKey", fundKey);
        countCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var investorsSql = new StringBuilder();
        AppendFundInvestorsBaseSelect(investorsSql);
        investorsSql.Append(" order by i.investor_name ");
        investorsSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var investorsCommand = new SqlCommand(investorsSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        investorsCommand.Parameters.AddWithValue("@fundKey", fundKey);
        investorsCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        investorsCommand.Parameters.AddWithValue("@offset", offset);
        investorsCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var investors = new List<(long InvestorKey, string InvestorName, string RelationshipName, string InvestorType, string ContactFirstName, string ContactLastName, string Status, DateTime? MemberSince, int? JoinYear)>();
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
                    investorsReader.GetStringOrEmpty("relationship_name"),
                    investorsReader.GetStringOrEmpty("investor_type_name"),
                    investorsReader.GetStringOrEmpty("contact_first_name"),
                    investorsReader.GetStringOrEmpty("contact_last_name"),
                    investorsReader.GetStringOrEmpty("investor_status"),
                    memberSince,
                    joinYear
                ));
            }
        }

        if (investors.Count == 0)
        {
            return new PagedResult<FundInvestorDto>
            {
                Items = [],
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount
            };
        }

        var aggregateSql = new StringBuilder();
        aggregateSql.Append(" select ");
        aggregateSql.Append(" x.investor_key, ");
        aggregateSql.Append(" isnull(comm.total_invested_amount, 0) as total_invested_amount, ");
        aggregateSql.Append(" isnull(inv.total_invested_fmv, 0) as total_invested_fmv ");
        var investorKeyParameters = new List<string>();
        for (var i = 0; i < investors.Count; i++)
        {
            investorKeyParameters.Add($"@investorKey{i}");
        }

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
        aggregateSql.Append($" where x.investor_key in ({string.Join(", ", investorKeyParameters)}) ");

        var totalsByInvestorKey = new Dictionary<long, (decimal TotalInvestedAmount, decimal TotalInvestedFmv)>();
        await using (var aggregateCommand = new SqlCommand(aggregateSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        })
        {
            aggregateCommand.Parameters.AddWithValue("@fundKey", fundKey);
            for (var i = 0; i < investors.Count; i++)
            {
                aggregateCommand.Parameters.AddWithValue(investorKeyParameters[i], investors[i].InvestorKey);
            }

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
                RelationshipName = investor.RelationshipName,
                InvestorType = investor.InvestorType,
                ContactFirstName = investor.ContactFirstName,
                ContactLastName = investor.ContactLastName,
                Status = investor.Status,
                TotalInvested = totals.TotalInvestedAmount,
                TotalInvestedFmv = totals.TotalInvestedFmv,
                MemberSince = investor.MemberSince,
                JoinYear = investor.JoinYear
            });
        }

        return new PagedResult<FundInvestorDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private static void AppendFundInvestorsBaseSelect(StringBuilder sql)
    {
        sql.Append(" select ");
        sql.Append(" i.investor_key, ");
        sql.Append(" i.investor_name, ");
        sql.Append(" isnull(i.relationship_name, '') as relationship_name, ");
        sql.Append(" isnull(i.investor_type_name, '') as investor_type_name, ");
        sql.Append(" isnull(i.contact_first_name, '') as contact_first_name, ");
        sql.Append(" isnull(i.contact_last_name, '') as contact_last_name, ");
        sql.Append(" case when isnull(i.is_current, 1) = 1 then 'Active' else 'Inactive' end as investor_status, ");
        sql.Append(" i.valid_from as member_since, ");
        sql.Append(" year(i.valid_from) as join_year ");
        sql.Append(" from ( ");
        sql.Append($" select distinct investor_key from {WarehouseTables.FactCommitted} where fund_key = @fundKey ");
        sql.Append(" union ");
        sql.Append($" select distinct investor_key from {WarehouseTables.FactInvestment} where fund_key = @fundKey ");
        sql.Append(" ) x ");
        sql.Append($" inner join {WarehouseTables.DimInvestor} i on i.investor_key = x.investor_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
        WarehouseSql.AppendInvestorSearchFilter(sql, "i");
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = @fundKey ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
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
            FundMetricSource.Investments => " order by fi.posted_date_key desc ",
            FundMetricSource.Distributions => " order by fd.posted_date_key desc ",
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
            or FundMetricSource.UnfundedCommitments)
        {
            sql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} q ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.quarter_year = q.quarter_year ");
            sql.Append(" where q.fund_key = @fundKey ");
            sql.Append(" group by d.quarter_year, d.calendar_year ");
        }
        else if (source is FundMetricSource.Investments)
        {
            sql.Append($" from {WarehouseTables.FactInvestment} fi ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fi.posted_date_key ");
            sql.Append(" where fi.fund_key = @fundKey ");
            sql.Append(" group by d.quarter_year, d.calendar_year ");
            sql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        }
        else if (source is FundMetricSource.Distributions)
        {
            sql.Append($" from {WarehouseTables.FactDistribution} fd ");
            AppendDimFundJoinOnFundKey(sql, "fd");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.posted_date_key ");
            sql.Append(" where fd.fund_key = @fundKey ");
            sql.Append(" group by d.quarter_year, d.calendar_year ");
            AppendDistributionTotalsHaving(sql);
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
            sql.Append(" fi.posted_date_key as date_key, ");
            sql.Append(" d.full_date, ");
        }
        else if (source is FundMetricSource.Distributions)
        {
            sql.Append(" fd.posted_date_key as date_key, ");
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
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fi.posted_date_key ");
            sql.Append(" where fi.fund_key = @fundKey ");
            sql.Append(" group by ");
            sql.Append(" fi.posted_date_key, d.full_date, d.quarter_year, d.calendar_year, d.month_year, ");
            sql.Append(" d.first_date_of_quater, d.last_date_of_quater ");
            sql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        }
        else if (source == FundMetricSource.Distributions)
        {
            sql.Append($" from {WarehouseTables.FactDistribution} fd ");
            AppendDimFundJoinOnFundKey(sql, "fd");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.posted_date_key ");
            sql.Append(" where fd.fund_key = @fundKey ");
            sql.Append(" group by ");
            sql.Append(" fd.posted_date_key, d.full_date, d.quarter_year, d.calendar_year, d.month_year, ");
            sql.Append(" d.first_date_of_quater, d.last_date_of_quater ");
            AppendDistributionTotalsHaving(sql);
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
        AppendFundCodeScalarSelect(pageSql);
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
            static reader => MapCommitmentRow(reader));
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
        AppendFundCodeScalarSelect(pageSql);
        pageSql.Append(" Period = quarter_year, ");
        pageSql.Append(" commitment_amount = sum(commitment_amount) ");
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
            static reader => MapCommitmentRow(reader));
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
        AppendFundCodeScalarSelect(pageSql);
        pageSql.Append(" fc.posted_date_key, ");
        pageSql.Append(" try_convert(date, cast(fc.posted_date_key as varchar(8)), 112) as full_date, ");
        pageSql.Append(" commitment_amount = sum(fc.committed_amount) ");
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
                var row = MapCommitmentRow(reader);
                return new FundGranularRowDto
                {
                    FundCode = row.FundCode,
                    Period = row.Period,
                    Date = reader.GetNullableDateTime("full_date"),
                    PostedDateKey = postedDateKey == 0 ? null : postedDateKey,
                    CommitmentAmount = row.CommitmentAmount,
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

    /// <summary>Filters dim_date quarter when facts join on posted_date_key (distributions) or date_key (NAV).</summary>
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

    private async Task<PagedResult<FundGranularRowDto>> GetFundInvestmentsLtdInternalAsync(int fundKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select case when exists ( ");
        countSql.Append(" select 1 ");
        countSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        countSql.Append(" where fi.fund_key = @fundKey ");
        countSql.Append(" ) then 1 else 0 end ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeScalarSelect(pageSql);
        pageSql.Append(" Period = 'Life To Date', ");
        pageSql.Append(" invested_amount = sum(isnull(fi.invested_amount, 0)), ");
        pageSql.Append(" Description = 'Total Investment' ");
        pageSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        pageSql.Append(" where fi.fund_key = @fundKey ");
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
            static reader => MapInvestmentRow(reader));
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
        countSql.Append(" select d.quarter_year ");
        countSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        countSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fi.posted_date_key ");
        countSql.Append(" where fi.fund_key = @fundKey ");
        AppendDimDateQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by d.quarter_year ");
        countSql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeScalarSelect(pageSql);
        pageSql.Append(" Period = d.quarter_year, ");
        pageSql.Append(" invested_amount = sum(isnull(fi.invested_amount, 0)) ");
        pageSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        pageSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fi.posted_date_key ");
        pageSql.Append(" where fi.fund_key = @fundKey ");
        AppendDimDateQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by d.quarter_year ");
        pageSql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
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
            static reader => MapInvestmentRow(reader));
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
        countSql.Append(" select fi.fund_key, fi.posted_date_key ");
        countSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        AppendInvestmentDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by fi.fund_key, fi.posted_date_key ");
        countSql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeScalarSelect(pageSql);
        pageSql.Append(" fi.posted_date_key, ");
        pageSql.Append(" try_convert(date, cast(fi.posted_date_key as varchar(8)), 112) as full_date, ");
        pageSql.Append(" invested_amount = sum(isnull(fi.invested_amount, 0)) ");
        pageSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        AppendInvestmentDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by fi.fund_key, fi.posted_date_key ");
        pageSql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        pageSql.Append(" order by fi.posted_date_key ");
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
                var row = MapInvestmentRow(reader);
                return new FundGranularRowDto
                {
                    FundCode = row.FundCode,
                    Period = row.Period,
                    Date = reader.GetNullableDateTime("full_date"),
                    PostedDateKey = postedDateKey == 0 ? null : postedDateKey,
                    InvestedAmount = row.InvestedAmount,
                    Description = string.Empty
                };
            });
    }

    private static void AppendInvestmentDailyPeriodJoinAndWhere(StringBuilder sql, FundPeriodFilter? period)
    {
        sql.Append(" where fi.fund_key = @fundKey ");
        if (period?.HasDateKey == true)
        {
            sql.Append(" and fi.posted_date_key = @dateKey ");
        }
    }

    private async Task<PagedResult<FundGranularRowDto>> GetFundDistributionsLtdInternalAsync(int fundKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select isnull(tt.transaction_type_name, '') as transaction_type ");
        countSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendCurrentTransactionTypeJoin(countSql, "fd");
        countSql.Append(" where fd.fund_key = @fundKey ");
        countSql.Append(" group by tt.transaction_type_name ");
        AppendDistributionTotalsHaving(countSql);
        countSql.Append(" ) ltd_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeScalarSelect(pageSql);
        pageSql.Append(" transaction_type = isnull(tt.transaction_type_name, ''), ");
        pageSql.Append(" Period = 'LTD', ");
        AppendDistributionAggregatedDateSelect(pageSql, hasDimDateJoin: false);
        AppendDistributionTotalsSelect(pageSql, "fd");
        pageSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendCurrentTransactionTypeJoin(pageSql, "fd");
        pageSql.Append(" where fd.fund_key = @fundKey ");
        pageSql.Append(" group by tt.transaction_type_name ");
        AppendDistributionTotalsHaving(pageSql);
        pageSql.Append(" order by tt.transaction_type_name ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => MapDistributionRowWithDate(reader));
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
        countSql.Append(" select isnull(tt.transaction_type_name, '') as transaction_type, d.quarter_year ");
        countSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendCurrentTransactionTypeJoin(countSql, "fd");
        countSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.posted_date_key ");
        countSql.Append(" where fd.fund_key = @fundKey ");
        AppendDimDateQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by tt.transaction_type_name, d.quarter_year ");
        AppendDistributionTotalsHaving(countSql);
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeScalarSelect(pageSql);
        pageSql.Append(" transaction_type = isnull(tt.transaction_type_name, ''), ");
        pageSql.Append(" Period = d.quarter_year, ");
        AppendDistributionAggregatedDateSelect(pageSql, hasDimDateJoin: true);
        AppendDistributionTotalsSelect(pageSql, "fd");
        pageSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendCurrentTransactionTypeJoin(pageSql, "fd");
        pageSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.posted_date_key ");
        pageSql.Append(" where fd.fund_key = @fundKey ");
        AppendDimDateQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by tt.transaction_type_name, d.quarter_year ");
        AppendDistributionTotalsHaving(pageSql);
        pageSql.Append(" order by tt.transaction_type_name, d.quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => MapDistributionRowWithDate(reader));
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
        countSql.Append(" select isnull(tt.transaction_type_name, '') as transaction_type, fd.posted_date_key ");
        countSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendCurrentTransactionTypeJoin(countSql, "fd");
        AppendDistributionDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by tt.transaction_type_name, fd.posted_date_key ");
        AppendDistributionTotalsHaving(countSql);
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeScalarSelect(pageSql);
        pageSql.Append(" transaction_type = isnull(tt.transaction_type_name, ''), ");
        pageSql.Append(" fd.posted_date_key, ");
        pageSql.Append(" try_convert(date, cast(fd.posted_date_key as varchar(8)), 112) as full_date, ");
        AppendDistributionTotalsSelect(pageSql, "fd");
        pageSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendCurrentTransactionTypeJoin(pageSql, "fd");
        AppendDistributionDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by tt.transaction_type_name, fd.posted_date_key ");
        AppendDistributionTotalsHaving(pageSql);
        pageSql.Append(" order by tt.transaction_type_name, fd.posted_date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteFundGranularPageQueryAsync(
            fundKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => MapDistributionRowWithDate(reader));
    }

    private static void AppendDistributionDailyPeriodJoinAndWhere(StringBuilder sql, FundPeriodFilter? period)
    {
        sql.Append(" where fd.fund_key = @fundKey ");
        if (period?.HasDateKey == true)
        {
            sql.Append(" and fd.posted_date_key = @dateKey ");
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

    private static void AppendFundCodeScalarSelect(StringBuilder sql)
    {
        sql.Append(" fund_code = ( ");
        sql.Append($" select top 1 isnull(fund_code, '') from {WarehouseTables.DimFund} f ");
        sql.Append(" where f.fund_key = @fundKey and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" ), ");
    }

    private static void AppendCurrentTransactionTypeJoin(StringBuilder sql, string factAlias)
    {
        sql.Append($" inner join {WarehouseTables.DimTransactionType} tt on tt.transaction_type_key = {factAlias}.transaction_type_key ");
        sql.Append(" and isnull(tt.is_current, 1) = 1 ");
    }

    private static void AppendDistributionAggregatedDateSelect(StringBuilder sql, bool hasDimDateJoin)
    {
        sql.Append(" max(fd.posted_date_key) as posted_date_key, ");
        if (hasDimDateJoin)
        {
            sql.Append(" max(d.full_date) as full_date, ");
        }
        else
        {
            sql.Append(" try_convert(date, cast(max(fd.posted_date_key) as varchar(8)), 112) as full_date, ");
        }
    }

    private static void AppendDistributionTotalsSelect(StringBuilder sql, string factAlias)
    {
        sql.Append($" units = sum(isnull({factAlias}.distributed_units, 0)), ");
        sql.Append($" amount = sum(isnull({factAlias}.distributed_amount, 0)) ");
    }

    private static void AppendDistributionTotalsHaving(StringBuilder sql)
    {
        sql.Append(" having sum(isnull(fd.distributed_amount, 0)) != 0 ");
        sql.Append(" or sum(isnull(fd.distributed_units, 0)) != 0 ");
    }

    private static FundGranularRowDto MapCommitmentRow(SqlDataReader reader)
    {
        var period = reader.GetNullableStringIfPresent("Period");
        if (string.IsNullOrEmpty(period) && reader.TryGetOrdinal("Period", out var periodOrdinal) && !reader.IsDBNull(periodOrdinal))
        {
            period = reader.GetString(periodOrdinal);
        }

        var description = reader.GetNullableStringIfPresent("Description") ?? string.Empty;
        if (string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(period) && !string.Equals(period, "Life To Date", StringComparison.OrdinalIgnoreCase))
        {
            description = $"{period} Commitment";
        }

        return new FundGranularRowDto
        {
            FundCode = reader.GetStringOrEmpty("fund_code"),
            Period = period,
            CommitmentAmount = reader.GetDecimalOrDefault("commitment_amount"),
            Description = description
        };
    }

    private static FundGranularRowDto MapInvestmentRow(SqlDataReader reader)
    {
        var period = reader.GetNullableStringIfPresent("Period");
        if (string.IsNullOrEmpty(period) && reader.TryGetOrdinal("Period", out var periodOrdinal) && !reader.IsDBNull(periodOrdinal))
        {
            period = reader.GetString(periodOrdinal);
        }

        var description = reader.GetNullableStringIfPresent("Description") ?? string.Empty;
        if (string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(period) && !string.Equals(period, "Life To Date", StringComparison.OrdinalIgnoreCase))
        {
            description = $"{period} Investment";
        }

        return new FundGranularRowDto
        {
            FundCode = reader.GetStringOrEmpty("fund_code"),
            Period = period,
            InvestedAmount = reader.GetDecimalOrDefault("invested_amount"),
            Description = description
        };
    }

    private static FundGranularRowDto MapDistributionRow(SqlDataReader reader)
    {
        var period = reader.GetNullableStringIfPresent("Period");
        if (string.IsNullOrEmpty(period) && reader.TryGetOrdinal("Period", out var periodOrdinal) && !reader.IsDBNull(periodOrdinal))
        {
            period = reader.GetString(periodOrdinal);
        }

        var transactionType = reader.GetNullableStringIfPresent("transaction_type");

        return new FundGranularRowDto
        {
            FundCode = reader.GetStringOrEmpty("fund_code"),
            TransactionType = transactionType,
            Period = period,
            Units = reader.GetDecimalOrDefault("units"),
            Amount = reader.GetDecimalOrDefault("amount")
        };
    }

    private static FundGranularRowDto MapDistributionRowWithDate(SqlDataReader reader)
    {
        var row = MapDistributionRow(reader);
        var postedDateKey = reader.GetInt32OrDefaultIfPresent("posted_date_key");
        return new FundGranularRowDto
        {
            FundCode = row.FundCode,
            TransactionType = row.TransactionType,
            Period = row.Period,
            Date = reader.GetNullableDateTimeIfPresent("full_date"),
            PostedDateKey = postedDateKey is > 0 ? postedDateKey : null,
            Amount = row.Amount,
            Units = row.Units
        };
    }

    private static string BuildDistributionPeriodDescription(FundGranularRowDto row)
    {
        if (!string.IsNullOrEmpty(row.Period))
        {
            return $"{row.Period} Distribution";
        }

        if (row.Date.HasValue)
        {
            return row.Date.Value.ToString("yyyy-MM-dd");
        }

        return string.Empty;
    }

    private static PagedResult<FundDistributionGroupDto> BuildGroupedDistributionPage(
        IReadOnlyList<FundGranularRowDto> flatRows,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var groups = flatRows
            .GroupBy(row => row.TransactionType ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var periods = group
                    .Select(row => new FundDistributionPeriodRowDto
                    {
                        Period = row.Period,
                        Date = row.Date,
                        PostedDateKey = row.PostedDateKey,
                        Amount = row.Amount,
                        Units = row.Units,
                        Description = BuildDistributionPeriodDescription(row)
                    })
                    .ToList();

                return new FundDistributionGroupDto
                {
                    FundCode = group.First().FundCode,
                    TransactionType = group.Key,
                    Periods = periods,
                    TotalAmount = periods.Sum(p => p.Amount ?? 0m),
                    TotalUnits = periods.Sum(p => p.Units ?? 0m)
                };
            })
            .ToList();

        var pagedGroups = groups.Skip(offset).Take(normalizedPageSize).ToList();

        return new PagedResult<FundDistributionGroupDto>
        {
            Items = pagedGroups,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = groups.Count
        };
    }

}
