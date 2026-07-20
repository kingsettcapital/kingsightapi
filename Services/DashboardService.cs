using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

/// <summary>Aggregated warehouse queries for the Kingsight dashboard widgets.</summary>
public sealed class DashboardService : IDashboardService
{
    private readonly string _connectionString;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IConfiguration configuration, ILogger<DashboardService> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        _logger = logger;
    }

    public async Task<DashboardResponseDto> GetDashboardAsync(
        int calendarYear,
        IReadOnlyList<string> widgetIds,
        CancellationToken cancellationToken = default)
    {
        var requested = widgetIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var widgets = new DashboardWidgetsDto();
            var needsKpi = requested.Overlaps(KpiWidgetIds);

            KpiSnapshot? kpi = null;
            if (needsKpi)
            {
                kpi = await LoadKpiSnapshotAsync(connection, calendarYear, cancellationToken);
            }

            if (requested.Contains(DashboardWidgetIds.PortfolioValue))
            {
                widgets = widgets with
                {
                    PortfolioValue = BuildPortfolioValueKpi(kpi!)
                };
            }

            if (requested.Contains(DashboardWidgetIds.ActiveFunds))
            {
                widgets = widgets with { ActiveFunds = BuildActiveFundsKpi(kpi!) };
            }

            if (requested.Contains(DashboardWidgetIds.TotalAum))
            {
                widgets = widgets with { TotalAum = BuildTotalAumKpi(kpi!) };
            }

            if (requested.Contains(DashboardWidgetIds.YtdReturns))
            {
                widgets = widgets with { YtdReturns = BuildYtdReturnsKpi(kpi!) };
            }

            if (requested.Contains(DashboardWidgetIds.InvestorCount))
            {
                widgets = widgets with { InvestorCount = BuildInvestorCountKpi(kpi!) };
            }

            if (requested.Contains(DashboardWidgetIds.AssetCount))
            {
                widgets = widgets with { AssetCount = BuildAssetCountKpi(kpi!) };
            }

            if (requested.Contains(DashboardWidgetIds.PerformanceChart))
            {
                widgets = widgets with
                {
                    PerformanceChart = await LoadPerformanceChartAsync(connection, calendarYear, cancellationToken)
                };
            }

            if (requested.Contains(DashboardWidgetIds.AssetAllocation))
            {
                widgets = widgets with
                {
                    AssetAllocation = await LoadAssetAllocationAsync(connection, cancellationToken)
                };
            }

            if (requested.Contains(DashboardWidgetIds.FundReturns))
            {
                widgets = widgets with
                {
                    FundReturns = await LoadFundReturnsChartAsync(connection, calendarYear, cancellationToken)
                };
            }

            if (requested.Contains(DashboardWidgetIds.InvestorGrowth))
            {
                widgets = widgets with
                {
                    InvestorGrowth = await LoadInvestorGrowthChartAsync(connection, cancellationToken)
                };
            }

            if (requested.Contains(DashboardWidgetIds.GeographicDistribution))
            {
                widgets = widgets with
                {
                    GeographicDistribution = await LoadGeographicDistributionAsync(connection, cancellationToken)
                };
            }

            _logger.LogInformation(
                "Dashboard loaded for {CalendarYear} with widgets {Widgets}.",
                calendarYear,
                string.Join(", ", requested));

            return new DashboardResponseDto
            {
                LastUpdated = DateTime.UtcNow,
                CalendarYear = calendarYear,
                Widgets = widgets
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dashboard load cancelled for {CalendarYear}.", calendarYear);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard for {CalendarYear}.", calendarYear);
            throw;
        }
    }

    private static readonly HashSet<string> KpiWidgetIds =
    [
        DashboardWidgetIds.PortfolioValue,
        DashboardWidgetIds.ActiveFunds,
        DashboardWidgetIds.TotalAum,
        DashboardWidgetIds.YtdReturns,
        DashboardWidgetIds.InvestorCount,
        DashboardWidgetIds.AssetCount
    ];

    private sealed class KpiSnapshot
    {
        public decimal PortfolioValue { get; init; }
        public decimal TotalAum { get; init; }
        public int ActiveFunds { get; init; }
        public int InvestorCount { get; init; }
        public int AssetCount { get; init; }
        public decimal? YtdReturnPercent { get; init; }
        public string? TopFundName { get; init; }
        public int? InvestorsAddedYtd { get; init; }
        public int? AssetsAddedYtd { get; init; }
        public int? FundsAddedYtd { get; init; }
    }

    // KPI snapshot — portfolio totals, counts, and YTD return from warehouse facts.
    private static async Task<KpiSnapshot> LoadKpiSnapshotAsync(
        SqlConnection connection,
        int calendarYear,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" portfolio_value = isnull(( ");
        sql.Append("   select sum(isnull(fi.invested_amount_fmv, 0)) ");
        sql.Append($"   from {WarehouseTables.FactInvestment} fi ");
        sql.Append($"   inner join {WarehouseTables.DimFund} f on fi.fund_key = f.fund_key ");
        sql.Append("   where ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" ), 0), ");
        // AUM — sum of net invested capital across all current funds (LTD portfolio facts).
        sql.Append(" total_aum = isnull(( ");
        sql.Append("   select sum(isnull(a.net_invested_capital_amount, 0)) ");
        sql.Append($"   from {WarehouseTables.FactInvestorPortfolioLtd} a ");
        sql.Append($"   inner join {WarehouseTables.DimFund} f on a.fund_key = f.fund_key ");
        sql.Append("   where ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" ), 0), ");
        sql.Append(" active_funds = isnull(( ");
        sql.Append("   select count(*) ");
        sql.Append($"   from {WarehouseTables.DimFund} f ");
        sql.Append("   where ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append("   and isnull(f.is_active, 0) = 1 ");
        sql.Append(" ), 0), ");
        sql.Append(" investor_count = isnull(( ");
        sql.Append("   select count(*) ");
        sql.Append($"   from {WarehouseTables.DimInvestor} i ");
        sql.Append("   where ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
        sql.Append(" ), 0), ");
        sql.Append(" asset_count = isnull(( ");
        sql.Append("   select count(*) ");
        sql.Append($"   from {WarehouseTables.DimProperty} p ");
        sql.Append("   where ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        WarehouseSql.AppendPropertyFundLevel000Filter(sql, "p");
        sql.Append(" ), 0), ");
        sql.Append(" invested_total = isnull(( ");
        sql.Append("   select sum(isnull(fi.invested_amount, 0)) ");
        sql.Append($"   from {WarehouseTables.FactInvestment} fi ");
        sql.Append($"   inner join {WarehouseTables.DimFund} f on fi.fund_key = f.fund_key ");
        sql.Append("   where ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" ), 0), ");
        sql.Append(" fmv_total = isnull(( ");
        sql.Append("   select sum(isnull(fi.invested_amount_fmv, 0)) ");
        sql.Append($"   from {WarehouseTables.FactInvestment} fi ");
        sql.Append($"   inner join {WarehouseTables.DimFund} f on fi.fund_key = f.fund_key ");
        sql.Append("   where ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" ), 0), ");
        sql.Append(" top_fund_name = ( ");
        sql.Append("   select top 1 isnull(f.fund_name, '') ");
        sql.Append($"   from {WarehouseTables.FactInvestorPortfolioLtd} a ");
        sql.Append($"   inner join {WarehouseTables.DimFund} f on a.fund_key = f.fund_key ");
        sql.Append("   where ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append("   group by f.fund_name ");
        sql.Append("   order by sum(isnull(a.net_invested_capital_amount, 0)) desc ");
        sql.Append(" ), ");
        sql.Append(" investors_added_ytd = isnull(( ");
        sql.Append("   select count(distinct inv.investor_key) ");
        sql.Append("   from ( ");
        sql.Append("     select investor_key, min(posted_date_key) as first_date_key ");
        sql.Append($"     from {WarehouseTables.FactCommitted} ");
        sql.Append("     group by investor_key ");
        sql.Append("   ) inv ");
        sql.Append($"   inner join {WarehouseTables.DimDate} d on inv.first_date_key = d.date_key ");
        sql.Append("   where d.calendar_year = @calendarYear ");
        sql.Append(" ), 0), ");
        sql.Append(" assets_added_ytd = isnull(( ");
        sql.Append("   select count(*) ");
        sql.Append($"   from {WarehouseTables.DimProperty} p ");
        sql.Append($"   inner join {WarehouseTables.DimDate} d on p.valid_from = d.full_date ");
        sql.Append("   where ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        WarehouseSql.AppendPropertyFundLevel000Filter(sql, "p");
        sql.Append("   and d.calendar_year = @calendarYear ");
        sql.Append(" ), 0) ");

        await using var command = new SqlCommand(sql.ToString(), connection);
        command.Parameters.AddWithValue("@calendarYear", calendarYear);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new KpiSnapshot();
        }

        var invested = reader.GetDecimalOrDefault("invested_total");
        var fmv = reader.GetDecimalOrDefault("fmv_total");
        decimal? ytdReturn = null;
        if (Math.Abs(invested) > 0)
        {
            ytdReturn = RoundPercent((fmv - invested) / Math.Abs(invested) * 100m);
        }

        return new KpiSnapshot
        {
            PortfolioValue = reader.GetDecimalOrDefault("portfolio_value"),
            TotalAum = reader.GetDecimalOrDefault("total_aum"),
            ActiveFunds = reader.GetInt32OrDefault("active_funds"),
            InvestorCount = reader.GetInt32OrDefault("investor_count"),
            AssetCount = reader.GetInt32OrDefault("asset_count"),
            YtdReturnPercent = ytdReturn,
            TopFundName = reader.GetNullableString("top_fund_name"),
            InvestorsAddedYtd = reader.GetInt32OrDefault("investors_added_ytd"),
            AssetsAddedYtd = reader.GetInt32OrDefault("assets_added_ytd")
        };
    }

    private static DashboardKpiWidgetDto BuildPortfolioValueKpi(KpiSnapshot kpi) =>
        new()
        {
            Value = kpi.TotalAum,
            YtdChangePercent = kpi.YtdReturnPercent,
            Subtitle = "Market Value",
            Format = "money"
        };

    private static DashboardKpiWidgetDto BuildActiveFundsKpi(KpiSnapshot kpi) =>
        new()
        {
            Value = kpi.ActiveFunds,
            YtdChange = kpi.FundsAddedYtd,
            Subtitle = "Under Management",
            Format = "count"
        };

    private static DashboardKpiWidgetDto BuildTotalAumKpi(KpiSnapshot kpi) =>
        new()
        {
            Value = kpi.TotalAum,
            YtdChangePercent = kpi.YtdReturnPercent,
            Subtitle = "Assets Under Management",
            Format = "money"
        };

    private static DashboardKpiWidgetDto BuildYtdReturnsKpi(KpiSnapshot kpi) =>
        new()
        {
            Value = kpi.YtdReturnPercent ?? 0m,
            Subtitle = string.IsNullOrWhiteSpace(kpi.TopFundName) ? "Portfolio" : kpi.TopFundName!,
            Format = "percent"
        };

    private static DashboardKpiWidgetDto BuildInvestorCountKpi(KpiSnapshot kpi) =>
        new()
        {
            Value = kpi.InvestorCount,
            YtdChange = kpi.InvestorsAddedYtd,
            Subtitle = "Active LPs",
            Format = "count"
        };

    private static DashboardKpiWidgetDto BuildAssetCountKpi(KpiSnapshot kpi) =>
        new()
        {
            Value = kpi.AssetCount,
            YtdChange = kpi.AssetsAddedYtd,
            Subtitle = "Across All Funds",
            Format = "count"
        };

    // Monthly NAV return % for top funds (performance vs benchmark chart).
    private static async Task<DashboardLineChartWidgetDto> LoadPerformanceChartAsync(
        SqlConnection connection,
        int calendarYear,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" f.fund_name, ");
        sql.Append(" isnull(d.month_year, '') as month_year, ");
        sql.Append(" sort_key = min(d.date_key), ");
        sql.Append(" nav_total = sum(isnull(n.nav, 0)) ");
        sql.Append($" from {WarehouseTables.FactFundNav} n ");
        sql.Append($" inner join {WarehouseTables.DimDate} d on n.date_key = d.date_key ");
        sql.Append($" inner join {WarehouseTables.DimFund} f on n.fund_key = f.fund_key ");
        sql.Append(" where d.calendar_year = @calendarYear ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" and f.fund_key in ( ");
        sql.Append("   select top 2 f2.fund_key ");
        sql.Append($"   from {WarehouseTables.FactInvestorPortfolioLtd} a ");
        sql.Append($"   inner join {WarehouseTables.DimFund} f2 on a.fund_key = f2.fund_key ");
        sql.Append("   where ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f2");
        sql.Append("   group by f2.fund_key ");
        sql.Append("   order by sum(isnull(a.net_invested_capital_amount, 0)) desc ");
        sql.Append(" ) ");
        sql.Append(" group by f.fund_name, d.month_year ");
        sql.Append(" order by f.fund_name, min(d.date_key) ");

        await using var command = new SqlCommand(sql.ToString(), connection);
        command.Parameters.AddWithValue("@calendarYear", calendarYear);

        var byFund = new Dictionary<string, SortedDictionary<int, (string MonthYear, decimal Nav)>>(
            StringComparer.OrdinalIgnoreCase);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fundName = reader.GetStringOrEmpty("fund_name");
            var sortKey = reader.GetInt32OrDefault("sort_key");
            var monthYear = reader.GetStringOrEmpty("month_year");
            var nav = reader.GetDecimalOrDefault("nav_total");

            if (!byFund.TryGetValue(fundName, out var months))
            {
                months = new SortedDictionary<int, (string, decimal)>();
                byFund[fundName] = months;
            }

            months[sortKey] = (monthYear, nav);
        }

        if (byFund.Count == 0)
        {
            return new DashboardLineChartWidgetDto();
        }

        var allMonths = byFund.Values
            .SelectMany(m => m.Keys)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        var categories = allMonths
            .Select(m => byFund.Values.FirstOrDefault(v => v.ContainsKey(m))?[m].MonthYear ?? m.ToString())
            .ToList();

        var series = new List<DashboardChartSeriesDto>();
        foreach (var (fundName, months) in byFund)
        {
            decimal? priorNav = null;
            var values = new List<decimal?>();
            foreach (var month in allMonths)
            {
                if (!months.TryGetValue(month, out var point))
                {
                    values.Add(null);
                    continue;
                }

                if (priorNav is > 0)
                {
                    values.Add(RoundPercent((point.Nav - priorNav.Value) / priorNav.Value * 100m));
                }
                else
                {
                    values.Add(0m);
                }

                priorNav = point.Nav;
            }

            series.Add(new DashboardChartSeriesDto { Name = fundName, Values = values });
        }

        return new DashboardLineChartWidgetDto { Categories = categories, Series = series };
    }

    private static async Task<DashboardDonutWidgetDto> LoadAssetAllocationAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" isnull(nullif(ltrim(rtrim(p.asset_type)), ''), 'Unknown') as asset_type, ");
        sql.Append(" property_count = count(*) ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        WarehouseSql.AppendPropertyFundLevel000Filter(sql, "p");
        sql.Append(" group by isnull(nullif(ltrim(rtrim(p.asset_type)), ''), 'Unknown') ");
        sql.Append(" order by property_count desc ");

        await using var command = new SqlCommand(sql.ToString(), connection);
        var slices = new List<DashboardDonutSliceDto>();
        var total = 0;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var count = reader.GetInt32OrDefault("property_count");
            total += count;
            slices.Add(new DashboardDonutSliceDto
            {
                Label = reader.GetStringOrEmpty("asset_type"),
                Value = count
            });
        }

        if (total > 0)
        {
            slices = slices
                .Select(s => new DashboardDonutSliceDto
                {
                    Label = s.Label,
                    Value = s.Value,
                    SharePercent = RoundPercent(s.Value / total * 100m)
                })
                .ToList();
        }

        return new DashboardDonutWidgetDto { Slices = slices };
    }

    // Quarterly return % per fund for the selected calendar year (grouped bar chart).
    private static async Task<DashboardGroupedBarWidgetDto> LoadFundReturnsChartAsync(
        SqlConnection connection,
        int calendarYear,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" f.fund_key, ");
        sql.Append(" isnull(f.fund_name, '') as fund_name, ");
        sql.Append(" isnull(q.quarter_year, '') as quarter_year, ");
        sql.Append(" d.calendar_year, ");
        sql.Append(" nic = sum(isnull(q.net_invested_capital_amount, 0)) ");
        sql.Append($" from {WarehouseTables.FactInvestorPortfolioQuarterly} q ");
        sql.Append($" inner join {WarehouseTables.DimFund} f on q.fund_key = f.fund_key ");
        sql.Append($" inner join {WarehouseTables.DimDate} d on q.quarter_year = d.quarter_year ");
        sql.Append(" where d.calendar_year = @calendarYear ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" group by f.fund_key, f.fund_name, q.quarter_year, d.calendar_year ");
        sql.Append(" order by f.fund_name, q.quarter_year ");

        await using var command = new SqlCommand(sql.ToString(), connection);
        command.Parameters.AddWithValue("@calendarYear", calendarYear);

        var byFund = new Dictionary<int, (string Name, Dictionary<int, decimal> Quarters)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fundKey = reader.GetInt32OrDefault("fund_key");
            var quarterYear = reader.GetStringOrEmpty("quarter_year");
            var calendarYearValue = reader.GetInt32OrDefault("calendar_year");
            var quarter = ParseQuarterNumber(quarterYear, calendarYearValue);
            var nic = reader.GetDecimalOrDefault("nic");

            if (!byFund.TryGetValue(fundKey, out var entry))
            {
                entry = (reader.GetStringOrEmpty("fund_name"), new Dictionary<int, decimal>());
                byFund[fundKey] = entry;
            }

            if (quarter is >= 1 and <= 4)
            {
                entry.Quarters[quarter] = nic;
            }
        }

        var topFunds = byFund
            .OrderByDescending(f => f.Value.Quarters.Values.Sum())
            .Take(5)
            .ToList();

        var categories = topFunds.Select(f => f.Value.Name).ToList();
        var quarterLabels = new[] { "Q1", "Q2", "Q3", "Q4" };
        var series = new List<DashboardChartSeriesDto>();

        for (var q = 1; q <= 4; q++)
        {
            var values = new List<decimal?>();
            foreach (var fund in topFunds)
            {
                if (!fund.Value.Quarters.TryGetValue(q, out var current))
                {
                    values.Add(null);
                    continue;
                }

                if (q == 1 || !fund.Value.Quarters.TryGetValue(q - 1, out var prior) || prior == 0)
                {
                    values.Add(0m);
                }
                else
                {
                    values.Add(RoundPercent((current - prior) / Math.Abs(prior) * 100m));
                }
            }

            series.Add(new DashboardChartSeriesDto { Name = quarterLabels[q - 1], Values = values });
        }

        return new DashboardGroupedBarWidgetDto { Categories = categories, Series = series };
    }

    private static async Task<DashboardLineChartWidgetDto> LoadInvestorGrowthChartAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" d.calendar_year, ");
        sql.Append(" new_investors = count(distinct inv.investor_key) ");
        sql.Append(" from ( ");
        sql.Append("   select investor_key, min(posted_date_key) as first_date_key ");
        sql.Append($"   from {WarehouseTables.FactCommitted} ");
        sql.Append("   group by investor_key ");
        sql.Append(" ) inv ");
        sql.Append($" inner join {WarehouseTables.DimDate} d on inv.first_date_key = d.date_key ");
        sql.Append(" group by d.calendar_year ");
        sql.Append(" order by d.calendar_year ");

        await using var command = new SqlCommand(sql.ToString(), connection);

        var years = new List<string>();
        var cumulative = new List<decimal?>();
        var runningTotal = 0;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            runningTotal += reader.GetInt32OrDefault("new_investors");
            years.Add(reader.GetInt32OrDefault("calendar_year").ToString());
            cumulative.Add(runningTotal);
        }

        return new DashboardLineChartWidgetDto
        {
            Categories = years,
            Series =
            [
                new DashboardChartSeriesDto
                {
                    Name = "Investors",
                    Values = cumulative
                }
            ]
        };
    }

    private static async Task<DashboardHorizontalBarWidgetDto> LoadGeographicDistributionAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" region = case ");
        sql.Append("   when upper(left(isnull(p.province, ''), 2)) in ('ON','BC','QC','AB') ");
        sql.Append("     then upper(left(isnull(p.province, ''), 2)) ");
        sql.Append("   when lower(isnull(p.province, '')) like 'ontario%' then 'ON' ");
        sql.Append("   when lower(isnull(p.province, '')) like 'british%' then 'BC' ");
        sql.Append("   when lower(isnull(p.province, '')) like 'quebec%' then 'QC' ");
        sql.Append("   when lower(isnull(p.province, '')) like 'alberta%' then 'AB' ");
        sql.Append("   else 'Other' ");
        sql.Append(" end, ");
        sql.Append(" property_count = count(*) ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        WarehouseSql.AppendPropertyFundLevel000Filter(sql, "p");
        sql.Append(" group by case ");
        sql.Append("   when upper(left(isnull(p.province, ''), 2)) in ('ON','BC','QC','AB') ");
        sql.Append("     then upper(left(isnull(p.province, ''), 2)) ");
        sql.Append("   when lower(isnull(p.province, '')) like 'ontario%' then 'ON' ");
        sql.Append("   when lower(isnull(p.province, '')) like 'british%' then 'BC' ");
        sql.Append("   when lower(isnull(p.province, '')) like 'quebec%' then 'QC' ");
        sql.Append("   when lower(isnull(p.province, '')) like 'alberta%' then 'AB' ");
        sql.Append("   else 'Other' ");
        sql.Append(" end ");
        sql.Append(" order by property_count desc ");

        await using var command = new SqlCommand(sql.ToString(), connection);
        var raw = new List<(string Label, int Count)>();
        var total = 0;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var count = reader.GetInt32OrDefault("property_count");
            raw.Add((reader.GetStringOrEmpty("region"), count));
            total += count;
        }

        var items = raw
            .Select(r => new DashboardHorizontalBarItemDto
            {
                Label = r.Label,
                SharePercent = total > 0 ? RoundPercent(r.Count / (decimal)total * 100m) : 0
            })
            .ToList();

        return new DashboardHorizontalBarWidgetDto { Items = items };
    }

    private static decimal RoundPercent(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static int ParseQuarterNumber(string quarterYear, int calendarYear)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            quarterYear,
            @"\bQ([1-4])\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var quarter))
        {
            return quarter;
        }

        if (calendarYear > 0
            && int.TryParse(quarterYear.Replace(calendarYear.ToString(), string.Empty).Trim(), out var trailing))
        {
            return trailing is >= 1 and <= 4 ? trailing : 0;
        }

        return 0;
    }
}
