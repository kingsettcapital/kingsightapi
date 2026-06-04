using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class InvestorPortalService
{
    private const int DistributionGroupingFetchCap = 10_000;


    public async Task<PagedResult<FundPeriodDto>> GetInvestorPeriodsAsync(
        long investorKey, TimeGranularity view, FundMetricSource source, int page, int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => CreateLtdAllPeriodsPage(page, pageSize),
                TimeGranularity.Quarterly => await GetInvestorPeriodsQuarterlyInternalAsync(investorKey, source, page, pageSize),
                TimeGranularity.Daily => await GetInvestorPeriodsDailyInternalAsync(investorKey, source, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} periods for investor {InvestorKey} cancelled", view, investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} periods for investor {InvestorKey}", view, investorKey);
            throw;
        }
    }

    public async Task<PagedResult<FundGranularRowDto>> GetInvestorCommitmentsAsync(
        long investorKey, TimeGranularity view, FundPeriodFilter? period, int page, int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => await GetInvestorCommitmentsLtdInternalAsync(investorKey, page, pageSize),
                TimeGranularity.Quarterly => await GetInvestorCommitmentsQuarterlyInternalAsync(investorKey, period, page, pageSize),
                TimeGranularity.Daily => await GetInvestorCommitmentsDailyInternalAsync(investorKey, period, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} commitments for investor {InvestorKey} cancelled", view, investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} commitments for investor {InvestorKey}", view, investorKey);
            throw;
        }
    }

    public async Task<PagedResult<FundGranularRowDto>> GetInvestorUnfundedCommitmentsAsync(
        long investorKey, TimeGranularity view, FundPeriodFilter? period, int page, int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => await GetInvestorUnfundedCommitmentsLtdInternalAsync(investorKey, page, pageSize),
                TimeGranularity.Quarterly => await GetInvestorUnfundedCommitmentsQuarterlyInternalAsync(investorKey, period, page, pageSize),
                TimeGranularity.Daily => await GetInvestorUnfundedCommitmentsDailyInternalAsync(investorKey, period, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} unfunded commitments for investor {InvestorKey} cancelled", view, investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} unfunded commitments for investor {InvestorKey}", view, investorKey);
            throw;
        }
    }

    public async Task<PagedResult<FundGranularRowDto>> GetInvestorInvestmentActivityAsync(
        long investorKey, TimeGranularity view, FundPeriodFilter? period, int page, int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => await GetInvestorInvestmentsLtdInternalAsync(investorKey, page, pageSize),
                TimeGranularity.Quarterly => await GetInvestorInvestmentsQuarterlyInternalAsync(investorKey, period, page, pageSize),
                TimeGranularity.Daily => await GetInvestorInvestmentsDailyInternalAsync(investorKey, period, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} investments for investor {InvestorKey} cancelled", view, investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} investments for investor {InvestorKey}", view, investorKey);
            throw;
        }
    }

    public async Task<PagedResult<FundDistributionGroupDto>> GetInvestorDistributionsAsync(
        long investorKey, TimeGranularity view, FundPeriodFilter? period, int page, int pageSize)
    {
        try
        {
            var flat = view switch
            {
                TimeGranularity.Ltd => await GetInvestorDistributionsLtdInternalAsync(investorKey, 1, DistributionGroupingFetchCap),
                TimeGranularity.Quarterly => await GetInvestorDistributionsQuarterlyInternalAsync(investorKey, period, 1, DistributionGroupingFetchCap),
                TimeGranularity.Daily => await GetInvestorDistributionsDailyInternalAsync(investorKey, period, 1, DistributionGroupingFetchCap),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
            return BuildGroupedDistributionPage(flat.Items, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} distributions for investor {InvestorKey} cancelled", view, investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} distributions for investor {InvestorKey}", view, investorKey);
            throw;
        }
    }

    public async Task<PagedResult<FundGranularRowDto>> GetInvestorNavAsync(
        long investorKey, TimeGranularity view, FundPeriodFilter? period, int page, int pageSize)
    {
        try
        {
            return view switch
            {
                TimeGranularity.Ltd => await GetInvestorNavLtdInternalAsync(investorKey, page, pageSize),
                TimeGranularity.Quarterly => await GetInvestorNavQuarterlyInternalAsync(investorKey, period, page, pageSize),
                TimeGranularity.Daily => await GetInvestorNavDailyInternalAsync(investorKey, period, page, pageSize),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported time granularity.")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} NAV for investor {InvestorKey} cancelled", view, investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} NAV for investor {InvestorKey}", view, investorKey);
            throw;
        }
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

    private async Task<PagedResult<FundPeriodDto>> GetInvestorPeriodsQuarterlyInternalAsync(
        long investorKey,
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

        return await ExecuteInvestorPeriodPageQueryAsync(investorKey, normalizedPage, normalizedPageSize, offset, countSql, pageSql);
    }

    private async Task<PagedResult<FundPeriodDto>> GetInvestorPeriodsDailyInternalAsync(
        long investorKey,
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

        return await ExecuteInvestorPeriodPageQueryAsync(investorKey, normalizedPage, normalizedPageSize, offset, countSql, pageSql);
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
            sql.Append(" where q.investor_key = @investorKey ");
            sql.Append(" group by d.quarter_year, d.calendar_year ");
        }
        else if (source is FundMetricSource.Investments)
        {
            sql.Append($" from {WarehouseTables.FactInvestment} fi ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fi.posted_date_key ");
            sql.Append(" where fi.investor_key = @investorKey ");
            sql.Append(" group by d.quarter_year, d.calendar_year ");
            sql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        }
        else if (source is FundMetricSource.Distributions)
        {
            sql.Append($" from {WarehouseTables.FactDistribution} fd ");
            AppendDimFundJoinForInvestorFacts(sql, "fd");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.posted_date_key ");
            sql.Append(" where fd.investor_key = @investorKey ");
            sql.Append(" group by d.quarter_year, d.calendar_year ");
            AppendDistributionTotalsHaving(sql);
        }
        else
        {
            sql.Append($" from {WarehouseTables.FactFundNav} n ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = n.date_key ");
            AppendInvestorFundKeysNavWhere(sql);
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
            sql.Append(" where fc.investor_key = @investorKey ");
            sql.Append(" group by ");
            sql.Append(" fc.posted_date_key, d.full_date, d.quarter_year, d.calendar_year, d.month_year, ");
            sql.Append(" d.first_date_of_quater, d.last_date_of_quater ");
            sql.Append(" having sum(fc.committed_amount) != 0 ");
        }
        else if (source == FundMetricSource.UnfundedCommitments)
        {
            sql.Append($" from {WarehouseTables.FactCommitted} fc ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fc.posted_date_key ");
            sql.Append(" where fc.investor_key = @investorKey ");
            sql.Append(" group by ");
            sql.Append(" fc.posted_date_key, d.full_date, d.quarter_year, d.calendar_year, d.month_year, ");
            sql.Append(" d.first_date_of_quater, d.last_date_of_quater ");
            sql.Append(" having sum(fc.committed_amount_fmv) != 0 ");
        }
        else if (source == FundMetricSource.Investments)
        {
            sql.Append($" from {WarehouseTables.FactInvestment} fi ");
            AppendDimFundJoinForInvestorFacts(sql, "fi");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fi.posted_date_key ");
            sql.Append(" where fi.investor_key = @investorKey ");
            sql.Append(" group by ");
            sql.Append(" fi.posted_date_key, d.full_date, d.quarter_year, d.calendar_year, d.month_year, ");
            sql.Append(" d.first_date_of_quater, d.last_date_of_quater ");
            sql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        }
        else if (source == FundMetricSource.Distributions)
        {
            sql.Append($" from {WarehouseTables.FactDistribution} fd ");
            AppendDimFundJoinForInvestorFacts(sql, "fd");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.posted_date_key ");
            sql.Append(" where fd.investor_key = @investorKey ");
            sql.Append(" group by ");
            sql.Append(" fd.posted_date_key, d.full_date, d.quarter_year, d.calendar_year, d.month_year, ");
            sql.Append(" d.first_date_of_quater, d.last_date_of_quater ");
            AppendDistributionTotalsHaving(sql);
        }
        else
        {
            sql.Append($" from {WarehouseTables.FactFundNav} n ");
            sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = n.date_key ");
            AppendInvestorFundKeysNavWhere(sql);
            sql.Append(" and isnull(n.nav, 0) != 0 ");
        }
    }

    private async Task<PagedResult<FundPeriodDto>> ExecuteInvestorPeriodPageQueryAsync(
        long investorKey,
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
        countCommand.Parameters.AddWithValue("@investorKey", investorKey);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        pageCommand.Parameters.AddWithValue("@investorKey", investorKey);
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

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorCommitmentsLtdInternalAsync(long investorKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select p.fund_key ");
        countSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} p ");
        AppendDimFundJoinOnPortfolio(countSql, "p");
        countSql.Append(" where p.investor_key = @investorKey ");
        countSql.Append(" group by p.fund_key, df.fund_code ");
        countSql.Append(" ) commitment_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" Period = 'Life To Date', ");
        pageSql.Append(" commitment_amount = sum(p.commitment_amount), ");
        pageSql.Append(" Description = 'Total Commitment as of Date' ");
        pageSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} p ");
        AppendDimFundJoinOnPortfolio(pageSql, "p");
        pageSql.Append(" where p.investor_key = @investorKey ");
        pageSql.Append(" group by p.fund_key, df.fund_code ");
        pageSql.Append(" order by df.fund_code ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => MapCommitmentRow(reader));
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorCommitmentsQuarterlyInternalAsync(
        long investorKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select p.fund_key, p.quarter_year ");
        countSql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} p ");
        AppendDimFundJoinOnPortfolio(countSql, "p");
        countSql.Append(" where p.investor_key = @investorKey ");
        AppendPortfolioQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by p.fund_key, df.fund_code, p.quarter_year ");
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" Period = p.quarter_year, ");
        pageSql.Append(" commitment_amount = sum(p.commitment_amount) ");
        pageSql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} p ");
        AppendDimFundJoinOnPortfolio(pageSql, "p");
        pageSql.Append(" where p.investor_key = @investorKey ");
        AppendPortfolioQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by p.fund_key, df.fund_code, p.quarter_year ");
        pageSql.Append(" order by df.fund_code, p.quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => MapCommitmentRow(reader));
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorCommitmentsDailyInternalAsync(
        long investorKey,
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
        AppendDimFundJoinForInvestorFacts(countSql, "fc");
        AppendInvestorCommitmentDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by fc.fund_key, df.fund_code, fc.posted_date_key ");
        countSql.Append(" having sum(fc.committed_amount) != 0 ");
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" fc.posted_date_key, ");
        pageSql.Append(" try_convert(date, cast(fc.posted_date_key as varchar(8)), 112) as full_date, ");
        pageSql.Append(" commitment_amount = sum(fc.committed_amount) ");
        pageSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        AppendDimFundJoinForInvestorFacts(pageSql, "fc");
        AppendInvestorCommitmentDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by fc.fund_key, df.fund_code, fc.posted_date_key ");
        pageSql.Append(" having sum(fc.committed_amount) != 0 ");
        pageSql.Append(" order by df.fund_code, fc.posted_date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
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

    private static void AppendInvestorCommitmentDailyPeriodJoinAndWhere(StringBuilder sql, FundPeriodFilter? period)
    {
        sql.Append(" where fc.investor_key = @investorKey ");
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

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorUnfundedCommitmentsLtdInternalAsync(long investorKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select p.fund_key ");
        countSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} p ");
        AppendDimFundJoinOnPortfolio(countSql, "p");
        countSql.Append(" where p.investor_key = @investorKey ");
        countSql.Append(" group by p.fund_key, df.fund_code ");
        countSql.Append(" ) unfunded_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" Period = 'Life To Date', ");
        pageSql.Append(" amount = sum(p.unfunded_amount), ");
        pageSql.Append(" Description = 'Total Unfunded Commitment' ");
        pageSql.Append($" from {WarehouseTables.FactInvestorPortfolioLtd} p ");
        AppendDimFundJoinOnPortfolio(pageSql, "p");
        pageSql.Append(" where p.investor_key = @investorKey ");
        pageSql.Append(" group by p.fund_key, df.fund_code ");
        pageSql.Append(" order by df.fund_code ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => new FundGranularRowDto
            {
                FundCode = reader.GetStringOrEmpty("fund_code"),
                Period = reader.GetStringOrEmpty("Period"),
                Amount = reader.GetDecimalOrDefault("amount"),
                Description = reader.GetStringOrEmpty("Description")
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorUnfundedCommitmentsQuarterlyInternalAsync(
        long investorKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select p.fund_key, p.quarter_year ");
        countSql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} p ");
        AppendDimFundJoinOnPortfolio(countSql, "p");
        countSql.Append(" where p.investor_key = @investorKey ");
        AppendPortfolioQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by p.fund_key, df.fund_code, p.quarter_year ");
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" Period = p.quarter_year, ");
        pageSql.Append(" amount = sum(p.unfunded_amount), ");
        pageSql.Append(" Description = 'Quarterly Unfunded' ");
        pageSql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} p ");
        AppendDimFundJoinOnPortfolio(pageSql, "p");
        pageSql.Append(" where p.investor_key = @investorKey ");
        AppendPortfolioQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by p.fund_key, df.fund_code, p.quarter_year ");
        pageSql.Append(" order by df.fund_code, p.quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
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
                    FundCode = reader.GetStringOrEmpty("fund_code"),
                    Period = periodLabel,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Description = string.IsNullOrEmpty(periodLabel)
                        ? reader.GetStringOrEmpty("Description")
                        : $"{periodLabel} Unfunded Commitment"
                };
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorUnfundedCommitmentsDailyInternalAsync(
        long investorKey,
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
        AppendDimFundJoinForInvestorFacts(countSql, "fc");
        AppendInvestorCommitmentDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by fc.fund_key, df.fund_code, fc.posted_date_key ");
        countSql.Append(" having sum(fc.committed_amount_fmv) != 0 ");
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" fc.posted_date_key, ");
        pageSql.Append(" try_convert(date, cast(fc.posted_date_key as varchar(8)), 112) as full_date, ");
        pageSql.Append(" amount = sum(fc.committed_amount_fmv) ");
        pageSql.Append($" from {WarehouseTables.FactCommitted} fc ");
        AppendDimFundJoinForInvestorFacts(pageSql, "fc");
        AppendInvestorCommitmentDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by fc.fund_key, df.fund_code, fc.posted_date_key ");
        pageSql.Append(" having sum(fc.committed_amount_fmv) != 0 ");
        pageSql.Append(" order by df.fund_code, fc.posted_date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
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
                    FundCode = reader.GetStringOrEmpty("fund_code"),
                    Date = reader.GetNullableDateTime("full_date"),
                    PostedDateKey = postedDateKey == 0 ? null : postedDateKey,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Description = "Remaining commitment"
                };
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorNavLtdInternalAsync(long investorKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select n.fund_key ");
        countSql.Append($" from {WarehouseTables.FactFundNav} n ");
        AppendInvestorFundKeysNavWhere(countSql);
        countSql.Append(" and isnull(n.nav, 0) != 0 ");
        countSql.Append(" group by n.fund_key ");
        countSql.Append(" ) nav_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" Period = 'Life To Date', ");
        pageSql.Append(" amount = sum(n.nav), ");
        pageSql.Append(" Description = 'Total NAV' ");
        pageSql.Append($" from {WarehouseTables.FactFundNav} n ");
        AppendDimFundJoinForInvestorFacts(pageSql, "n");
        AppendInvestorFundKeysNavWhere(pageSql);
        pageSql.Append(" and isnull(n.nav, 0) != 0 ");
        pageSql.Append(" group by n.fund_key, df.fund_code ");
        pageSql.Append(" order by df.fund_code ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => new FundGranularRowDto
            {
                FundCode = reader.GetStringOrEmpty("fund_code"),
                Period = reader.GetStringOrEmpty("Period"),
                Amount = reader.GetDecimalOrDefault("amount"),
                Description = reader.GetStringOrEmpty("Description")
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorNavQuarterlyInternalAsync(
        long investorKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select n.fund_key, d.quarter_year ");
        countSql.Append($" from {WarehouseTables.FactFundNav} n ");
        countSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = n.date_key ");
        AppendInvestorFundKeysNavWhere(countSql);
        AppendDimDateQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" and isnull(n.nav, 0) != 0 ");
        countSql.Append(" group by n.fund_key, d.quarter_year ");
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" Period = d.quarter_year, ");
        pageSql.Append(" amount = sum(n.nav), ");
        pageSql.Append(" Description = 'Quarterly NAV' ");
        pageSql.Append($" from {WarehouseTables.FactFundNav} n ");
        pageSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = n.date_key ");
        AppendDimFundJoinForInvestorFacts(pageSql, "n");
        AppendInvestorFundKeysNavWhere(pageSql);
        AppendDimDateQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" and isnull(n.nav, 0) != 0 ");
        pageSql.Append(" group by n.fund_key, df.fund_code, d.quarter_year ");
        pageSql.Append(" order by df.fund_code, d.quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
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
                    FundCode = reader.GetStringOrEmpty("fund_code"),
                    Period = periodLabel,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Description = string.IsNullOrEmpty(periodLabel) ? reader.GetStringOrEmpty("Description") : $"{periodLabel} NAV"
                };
            });
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorNavDailyInternalAsync(
        long investorKey,
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
        AppendInvestorNavDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by n.fund_key, n.date_key ");
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" n.date_key, ");
        pageSql.Append(" try_convert(date, cast(n.date_key as varchar(8)), 112) as nav_date, ");
        pageSql.Append(" amount = sum(n.nav) ");
        pageSql.Append($" from {WarehouseTables.FactFundNav} n ");
        AppendDimFundJoinForInvestorFacts(pageSql, "n");
        AppendInvestorNavDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by n.fund_key, df.fund_code, n.date_key ");
        pageSql.Append(" order by df.fund_code, n.date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
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
                    FundCode = reader.GetStringOrEmpty("fund_code"),
                    Date = reader.GetNullableDateTime("nav_date"),
                    PostedDateKey = dateKey == 0 ? null : dateKey,
                    Amount = reader.GetDecimalOrDefault("amount"),
                    Description = string.Empty
                };
            });
    }

    private static void AppendInvestorFundKeysNavWhere(StringBuilder sql, string navAlias = "n")
    {
        sql.Append($" where {navAlias}.fund_key in ( ");
        sql.Append($" select fund_key from {WarehouseTables.FactCommitted} where investor_key = @investorKey ");
        sql.Append(" union ");
        sql.Append($" select fund_key from {WarehouseTables.FactInvestment} where investor_key = @investorKey ");
        sql.Append(" ) ");
    }

    private static void AppendInvestorNavDailyPeriodJoinAndWhere(StringBuilder sql, FundPeriodFilter? period)
    {
        AppendInvestorFundKeysNavWhere(sql);
        sql.Append(" and isnull(n.nav, 0) != 0 ");
        if (period?.HasDateKey == true)
        {
            sql.Append(" and n.date_key = @dateKey ");
        }
    }

    private static void AppendDimFundJoinForInvestorFacts(StringBuilder sql, string factAlias)
    {
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = {factAlias}.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
    }

    private static void AppendDimFundJoinOnPortfolio(StringBuilder sql, string portfolioAlias)
    {
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = {portfolioAlias}.fund_key ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
    }

    private static void AppendFundCodeColumnSelect(StringBuilder sql, string fundAlias = "df")
    {
        sql.Append($" isnull({fundAlias}.fund_code, '') as fund_code, ");
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

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorInvestmentsLtdInternalAsync(long investorKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select fi.fund_key ");
        countSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        AppendDimFundJoinForInvestorFacts(countSql, "fi");
        countSql.Append(" where fi.investor_key = @investorKey ");
        countSql.Append(" group by fi.fund_key, df.fund_code ");
        countSql.Append(" ) investment_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" Period = 'Life To Date', ");
        pageSql.Append(" invested_amount = sum(isnull(fi.invested_amount, 0)), ");
        pageSql.Append(" Description = 'Total Investment' ");
        pageSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        AppendDimFundJoinForInvestorFacts(pageSql, "fi");
        pageSql.Append(" where fi.investor_key = @investorKey ");
        pageSql.Append(" group by fi.fund_key, df.fund_code ");
        pageSql.Append(" order by df.fund_code ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => MapInvestmentRow(reader));
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorInvestmentsQuarterlyInternalAsync(
        long investorKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select fi.fund_key, d.quarter_year ");
        countSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        AppendDimFundJoinForInvestorFacts(countSql, "fi");
        countSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fi.posted_date_key ");
        countSql.Append(" where fi.investor_key = @investorKey ");
        AppendDimDateQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by fi.fund_key, df.fund_code, d.quarter_year ");
        countSql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" Period = d.quarter_year, ");
        pageSql.Append(" invested_amount = sum(isnull(fi.invested_amount, 0)) ");
        pageSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        AppendDimFundJoinForInvestorFacts(pageSql, "fi");
        pageSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fi.posted_date_key ");
        pageSql.Append(" where fi.investor_key = @investorKey ");
        AppendDimDateQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by fi.fund_key, df.fund_code, d.quarter_year ");
        pageSql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        pageSql.Append(" order by df.fund_code, d.quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => MapInvestmentRow(reader));
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorInvestmentsDailyInternalAsync(
        long investorKey,
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
        AppendDimFundJoinForInvestorFacts(countSql, "fi");
        AppendInvestorInvestmentDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by fi.fund_key, df.fund_code, fi.posted_date_key ");
        countSql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" fi.posted_date_key, ");
        pageSql.Append(" try_convert(date, cast(fi.posted_date_key as varchar(8)), 112) as full_date, ");
        pageSql.Append(" invested_amount = sum(isnull(fi.invested_amount, 0)) ");
        pageSql.Append($" from {WarehouseTables.FactInvestment} fi ");
        AppendDimFundJoinForInvestorFacts(pageSql, "fi");
        AppendInvestorInvestmentDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by fi.fund_key, df.fund_code, fi.posted_date_key ");
        pageSql.Append(" having sum(isnull(fi.invested_amount, 0)) != 0 ");
        pageSql.Append(" order by df.fund_code, fi.posted_date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
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

    private static void AppendInvestorInvestmentDailyPeriodJoinAndWhere(StringBuilder sql, FundPeriodFilter? period)
    {
        sql.Append(" where fi.investor_key = @investorKey ");
        if (period?.HasDateKey == true)
        {
            sql.Append(" and fi.posted_date_key = @dateKey ");
        }
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorDistributionsLtdInternalAsync(long investorKey, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select isnull(tt.transaction_type_name, '') as transaction_type, fd.fund_key ");
        countSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinForInvestorFacts(countSql, "fd");
        AppendCurrentTransactionTypeJoin(countSql, "fd");
        countSql.Append(" where fd.investor_key = @investorKey ");
        countSql.Append(" group by tt.transaction_type_name, fd.fund_key, df.fund_code ");
        AppendDistributionTotalsHaving(countSql);
        countSql.Append(" ) ltd_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" transaction_type = isnull(tt.transaction_type_name, ''), ");
        pageSql.Append(" Period = 'LTD', ");
        AppendDistributionAggregatedDateSelect(pageSql, hasDimDateJoin: false);
        AppendDistributionTotalsSelect(pageSql, "fd");
        pageSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinForInvestorFacts(pageSql, "fd");
        AppendCurrentTransactionTypeJoin(pageSql, "fd");
        pageSql.Append(" where fd.investor_key = @investorKey ");
        pageSql.Append(" group by tt.transaction_type_name, fd.fund_key, df.fund_code ");
        AppendDistributionTotalsHaving(pageSql);
        pageSql.Append(" order by df.fund_code, tt.transaction_type_name ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
            null,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => MapDistributionRowWithDate(reader));
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorDistributionsQuarterlyInternalAsync(
        long investorKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select isnull(tt.transaction_type_name, '') as transaction_type, fd.fund_key, d.quarter_year ");
        countSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinForInvestorFacts(countSql, "fd");
        AppendCurrentTransactionTypeJoin(countSql, "fd");
        countSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.posted_date_key ");
        countSql.Append(" where fd.investor_key = @investorKey ");
        AppendDimDateQuarterlyPeriodFilter(countSql, period);
        countSql.Append(" group by tt.transaction_type_name, fd.fund_key, df.fund_code, d.quarter_year ");
        AppendDistributionTotalsHaving(countSql);
        countSql.Append(" ) quarterly_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" transaction_type = isnull(tt.transaction_type_name, ''), ");
        pageSql.Append(" Period = d.quarter_year, ");
        AppendDistributionAggregatedDateSelect(pageSql, hasDimDateJoin: true);
        AppendDistributionTotalsSelect(pageSql, "fd");
        pageSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinForInvestorFacts(pageSql, "fd");
        AppendCurrentTransactionTypeJoin(pageSql, "fd");
        pageSql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = fd.posted_date_key ");
        pageSql.Append(" where fd.investor_key = @investorKey ");
        AppendDimDateQuarterlyPeriodFilter(pageSql, period);
        pageSql.Append(" group by tt.transaction_type_name, fd.fund_key, df.fund_code, d.quarter_year ");
        AppendDistributionTotalsHaving(pageSql);
        pageSql.Append(" order by df.fund_code, tt.transaction_type_name, d.quarter_year ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => MapDistributionRowWithDate(reader));
    }

    private async Task<PagedResult<FundGranularRowDto>> GetInvestorDistributionsDailyInternalAsync(
        long investorKey,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append(" from ( ");
        countSql.Append(" select isnull(tt.transaction_type_name, '') as transaction_type, fd.fund_key, fd.posted_date_key ");
        countSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinForInvestorFacts(countSql, "fd");
        AppendCurrentTransactionTypeJoin(countSql, "fd");
        AppendInvestorDistributionDailyPeriodJoinAndWhere(countSql, period);
        countSql.Append(" group by tt.transaction_type_name, fd.fund_key, df.fund_code, fd.posted_date_key ");
        AppendDistributionTotalsHaving(countSql);
        countSql.Append(" ) daily_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        AppendFundCodeColumnSelect(pageSql);
        pageSql.Append(" transaction_type = isnull(tt.transaction_type_name, ''), ");
        pageSql.Append(" fd.posted_date_key, ");
        pageSql.Append(" try_convert(date, cast(fd.posted_date_key as varchar(8)), 112) as full_date, ");
        AppendDistributionTotalsSelect(pageSql, "fd");
        pageSql.Append($" from {WarehouseTables.FactDistribution} fd ");
        AppendDimFundJoinForInvestorFacts(pageSql, "fd");
        AppendCurrentTransactionTypeJoin(pageSql, "fd");
        AppendInvestorDistributionDailyPeriodJoinAndWhere(pageSql, period);
        pageSql.Append(" group by tt.transaction_type_name, fd.fund_key, df.fund_code, fd.posted_date_key ");
        AppendDistributionTotalsHaving(pageSql);
        pageSql.Append(" order by df.fund_code, tt.transaction_type_name, fd.posted_date_key ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorGranularPageQueryAsync(
            investorKey,
            period,
            normalizedPage,
            normalizedPageSize,
            offset,
            countSql,
            pageSql,
            static reader => MapDistributionRowWithDate(reader));
    }

    private static void AppendInvestorDistributionDailyPeriodJoinAndWhere(StringBuilder sql, FundPeriodFilter? period)
    {
        sql.Append(" where fd.investor_key = @investorKey ");
        if (period?.HasDateKey == true)
        {
            sql.Append(" and fd.posted_date_key = @dateKey ");
        }
    }

    private async Task<PagedResult<FundGranularRowDto>> ExecuteInvestorGranularPageQueryAsync(
        long investorKey,
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
        AddInvestorGranularPeriodParameters(countCommand, investorKey, period);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddInvestorGranularPeriodParameters(pageCommand, investorKey, period);
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

    private static void AddInvestorGranularPeriodParameters(SqlCommand command, long investorKey, FundPeriodFilter? period)
    {
        command.Parameters.AddWithValue("@investorKey", investorKey);
        command.Parameters.AddWithValue("@dateKey", (object?)period?.DateKey ?? DBNull.Value);
    }

    private static void AppendInvestorCodeScalarSelect(StringBuilder sql)
    {
        sql.Append(" investor_code = ( ");
        sql.Append($" select top 1 isnull(cast(i.investor_id as varchar(20)), '') from {WarehouseTables.DimInvestor} i ");
        sql.Append(" where i.investor_key = @investorKey and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
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

    private static PagedResult<FundDistributionGroupDto> BuildGroupedDistributionPage(
        IReadOnlyList<FundGranularRowDto> flatRows,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var groups = flatRows
            .GroupBy(row => (TransactionType: row.TransactionType ?? string.Empty, row.FundCode))
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
                    FundCode = group.Key.FundCode,
                    TransactionType = group.Key.TransactionType,
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
