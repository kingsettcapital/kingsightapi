using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class DashboardService
{
    private async Task<DashboardSectionDataDto> BuildInvestorsKpiSummaryAsync(
        TimeGranularity view,
        DashboardSectionRegistry.SectionDefinition definition)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            select
                investor_count = (
                    select count(distinct i.investor_key)
                    from dbo.dim_investor i
                    where isnull(i.is_current, 1) = 1
                ),
                total_commitments = isnull((
                    select sum(isnull(fc.committed_amount, 0))
                    from dbo.fact_commitment fc
                    inner join dbo.dim_fund df on df.fund_key = fc.fund_key
                    where (
                        isnull(df.is_current, 1) = 1
                        or (
                            df.is_current is null
                            and getdate() between df.valid_from
                                and isnull(df.valid_to, cast('9999-12-31' as datetime2))
                        )
                    )
                ), 0),
                current_nav = isnull((
                    select sum(isnull(p.net_invested_capital_amount, 0))
                    from dbo.fact_investor_portfolio_ltd p
                    inner join dbo.dim_investor i on i.investor_key = p.investor_key
                    where isnull(i.is_current, 1) = 1
                ), 0),
                distributions_paid = isnull((
                    select sum(isnull(fd.distributed_amount, 0))
                    from dbo.fact_distribution fd
                ), 0),
                new_investors = (
                    select count(distinct i.investor_key)
                    from dbo.dim_investor i
                    where isnull(i.is_current, 1) = 1
                      and year(isnull(i.valid_from, getdate())) = year(getdate())
                )
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return BuildSectionData(definition, view, kpis: []);
        }

        var investorCount = reader.GetInt32OrDefault("investor_count");
        var totalCommitments = reader.GetDecimalOrDefault("total_commitments");
        var currentNav = reader.GetDecimalOrDefault("current_nav");
        var distributionsPaid = reader.GetDecimalOrDefault("distributions_paid");
        var newInvestors = reader.GetInt32OrDefault("new_investors");

        return BuildSectionData(
            definition,
            view,
            kpis:
            [
                Kpi("totalInvestors", "Total Investors", investorCount, FieldDataTypes.Integer, caption: "Active LPs"),
                Kpi("totalCommitments", "Total Commitments", totalCommitments, FieldDataTypes.Money, caption: "Cumulative"),
                Kpi("currentNav", "Current NAV", currentNav, FieldDataTypes.Money, caption: "Portfolio Value"),
                Kpi("distributionsPaid", "Distributions Paid", distributionsPaid, FieldDataTypes.Money, caption: "Cash Returns"),
                Kpi("weightedAvgIrr", "Weighted Avg IRR", null, FieldDataTypes.Percent, caption: "Net of Fees"),
                Kpi("newInvestors", "New Investors", newInvestors, FieldDataTypes.Integer, caption: "Period Additions")
            ]);
    }

    private async Task<DashboardSectionDataDto> BuildInvestorsAnalyticsBenchmarkingAsync(
        TimeGranularity view,
        DashboardSectionRegistry.SectionDefinition definition)
    {
        var byType = await QueryInvestorBreakdownAsync(
            """
            select
                label = isnull(nullif(ltrim(rtrim(i.investor_type_name)), ''), 'Other'),
                amount = sum(isnull(p.net_invested_capital_amount, 0))
            from dbo.fact_investor_portfolio_ltd p
            inner join dbo.dim_investor i on i.investor_key = p.investor_key
            where isnull(i.is_current, 1) = 1
            group by isnull(nullif(ltrim(rtrim(i.investor_type_name)), ''), 'Other')
            """);

        var byGeography = await QueryInvestorBreakdownAsync(
            """
            select
                label = isnull(nullif(ltrim(rtrim(i.country)), ''), 'Other'),
                amount = sum(isnull(p.net_invested_capital_amount, 0))
            from dbo.fact_investor_portfolio_ltd p
            inner join dbo.dim_investor i on i.investor_key = p.investor_key
            where isnull(i.is_current, 1) = 1
            group by isnull(nullif(ltrim(rtrim(i.country)), ''), 'Other')
            """);

        var concentration = await QueryInvestorConcentrationAsync();

        return BuildSectionData(
            definition,
            view,
            groups:
            [
                Group("By Investor Type", byType),
                Group("By Geography", byGeography),
                Group("Concentration", concentration)
            ]);
    }

    private async Task<DashboardSectionDataDto> BuildInvestorsCapitalAccountSummaryAsync(
        TimeGranularity view,
        DashboardSectionRegistry.SectionDefinition definition)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            select
                total_capital_raised = isnull((
                    select sum(isnull(fc.committed_amount, 0))
                    from dbo.fact_commitment fc
                ), 0),
                uncalled_capital = isnull((
                    select sum(isnull(p.unfunded_amount, 0))
                    from dbo.fact_investor_portfolio_ltd p
                ), 0),
                total_capital_deployed = isnull((
                    select sum(isnull(fi.invested_amount, 0))
                    from dbo.fact_investment fi
                ), 0),
                total_distributions_paid = isnull((
                    select sum(isnull(fd.distributed_amount, 0))
                    from dbo.fact_distribution fd
                ), 0)
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return BuildSectionData(definition, view, fields: []);
        }

        var totalRaised = reader.GetDecimalOrDefault("total_capital_raised");
        var uncalled = reader.GetDecimalOrDefault("uncalled_capital");
        var deployed = reader.GetDecimalOrDefault("total_capital_deployed");
        var distributions = reader.GetDecimalOrDefault("total_distributions_paid");
        var reinvested = 0m;
        var netReturned = distributions - reinvested;

        return BuildSectionData(
            definition,
            view,
            fields:
            [
                Field("totalCapitalRaisedLtd", DisplayFieldBuilder.Money(totalRaised)),
                Field("uncalledCapital", DisplayFieldBuilder.Money(uncalled)),
                Field("totalCapitalDeployed", DisplayFieldBuilder.Money(deployed)),
                Field("totalDistributionsPaid", DisplayFieldBuilder.Money(distributions)),
                Field("reinvestedDistributions", DisplayFieldBuilder.Money(reinvested)),
                Field("netCapitalReturned", DisplayFieldBuilder.Money(netReturned))
            ]);
    }

    private async Task<List<DynamicFieldDto>> QueryInvestorBreakdownAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        var rows = new List<(string Label, decimal Amount)>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                rows.Add((reader.GetStringOrEmpty("label"), reader.GetDecimalOrDefault("amount")));
            }
        }

        var total = rows.Sum(r => r.Amount);
        if (total <= 0m)
        {
            return
            [
                DisplayFieldBuilder.ToDynamicField("status", DisplayFieldBuilder.Text("No data available"))
            ];
        }

        return rows
            .OrderByDescending(r => r.Amount)
            .Select(r => DisplayFieldBuilder.ToDynamicField(
                ToFieldKey(r.Label),
                DisplayFieldBuilder.Percent(Math.Round(r.Amount / total * 100m, 1))))
            .ToList();
    }

    private async Task<List<DynamicFieldDto>> QueryInvestorConcentrationAsync()
    {
        const string sql = """
            with investor_aum as (
                select
                    i.investor_name,
                    aum = sum(isnull(p.net_invested_capital_amount, 0)),
                    commitment = sum(isnull(fc.committed_amount, 0))
                from dbo.dim_investor i
                left join dbo.fact_investor_portfolio_ltd p on p.investor_key = i.investor_key
                left join dbo.fact_commitment fc on fc.investor_key = i.investor_key
                where isnull(i.is_current, 1) = 1
                group by i.investor_name
            ),
            ranked as (
                select
                    investor_name,
                    aum,
                    commitment,
                    row_number() over (order by aum desc) as aum_rank
                from investor_aum
                where aum > 0
            ),
            totals as (
                select
                    total_aum = sum(aum),
                    total_commitment = sum(commitment),
                    investor_count = count(*),
                    largest_lp = max(case when aum_rank = 1 then commitment else 0 end)
                from ranked
            )
            select
                top5_pct = case when total_aum = 0 then 0
                    else (select sum(aum) from ranked where aum_rank <= 5) * 100.0 / total_aum end,
                top10_pct = case when total_aum = 0 then 0
                    else (select sum(aum) from ranked where aum_rank <= 10) * 100.0 / total_aum end,
                avg_commitment = case when investor_count = 0 then 0
                    else total_commitment / investor_count end,
                largest_lp = largest_lp
            from totals
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return
            [
                DisplayFieldBuilder.ToDynamicField("status", DisplayFieldBuilder.Text("No data available"))
            ];
        }

        return
        [
            DisplayFieldBuilder.ToDynamicField(
                "top5InvestorsAum",
                DisplayFieldBuilder.Percent(reader.GetNullableDecimal("top5_pct"))),
            DisplayFieldBuilder.ToDynamicField(
                "top10InvestorsAum",
                DisplayFieldBuilder.Percent(reader.GetNullableDecimal("top10_pct"))),
            DisplayFieldBuilder.ToDynamicField(
                "avgCommitment",
                DisplayFieldBuilder.Money(reader.GetDecimalOrDefault("avg_commitment"))),
            DisplayFieldBuilder.ToDynamicField(
                "largestLp",
                DisplayFieldBuilder.Money(reader.GetDecimalOrDefault("largest_lp")))
        ];
    }

    private async Task<PagedResult<DashboardTransactionDto>> GetInvestorTransactionsLtdAsync(int page, int pageSize) =>
        await GetInvestorTransactionsInternalAsync(page, pageSize, periodFilter: null);

    private async Task<PagedResult<DashboardTransactionDto>> GetInvestorTransactionsQuarterlyAsync(int page, int pageSize) =>
        await GetInvestorTransactionsInternalAsync(page, pageSize, periodFilter: "quarterly");

    private async Task<PagedResult<DashboardTransactionDto>> GetInvestorTransactionsDailyAsync(int page, int pageSize) =>
        await GetInvestorTransactionsInternalAsync(page, pageSize, periodFilter: "daily");

    private async Task<PagedResult<DashboardTransactionDto>> GetInvestorTransactionsInternalAsync(
        int page,
        int pageSize,
        string? periodFilter)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) from ( ");
        AppendInvestorTransactionsUnion(countSql, periodFilter);
        countSql.Append(" ) transaction_rows ");

        var pageSql = new StringBuilder();
        pageSql.Append(" select * from ( ");
        AppendInvestorTransactionsUnion(pageSql, periodFilter);
        pageSql.Append(" ) transaction_rows ");
        pageSql.Append(" order by activity_date desc, fund_name, description ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var countCommand = new SqlCommand(countSql.ToString(), connection);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection);
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<DashboardTransactionDto>();
        await using (var reader = await pageCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                items.Add(new DashboardTransactionDto
                {
                    Date = reader.GetNullableDateTimeFlexible("activity_date"),
                    Type = reader.GetStringOrEmpty("activity_type"),
                    Description = reader.GetStringOrEmpty("description"),
                    Amount = reader.GetDecimalOrDefault("amount"),
                    FundName = reader.GetStringOrEmpty("fund_name"),
                    Status = "Completed"
                });
            }
        }

        return new PagedResult<DashboardTransactionDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private static void AppendInvestorTransactionsUnion(StringBuilder sql, string? periodFilter)
    {
        sql.Append(" select ");
        sql.Append(" activity_date = try_convert(date, cast(fi.calculation_date_key as varchar(8)), 112), ");
        sql.Append(" activity_type = 'Acquisition', ");
        sql.Append(" description = isnull(df.fund_name, 'Investment'), ");
        sql.Append(" amount = isnull(fi.invested_amount, 0), ");
        sql.Append(" fund_name = isnull(df.fund_name, '') ");
        sql.Append($" from {WarehouseTables.FactInvestment} fi ");
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fi.fund_key ");
        sql.Append(" where isnull(fi.invested_amount, 0) <> 0 ");

        if (periodFilter == "quarterly")
        {
            sql.Append(" and fi.calculation_date_key >= convert(int, convert(varchar(8), dateadd(quarter, -4, getdate()), 112)) ");
        }
        else if (periodFilter == "daily")
        {
            sql.Append(" and fi.calculation_date_key >= convert(int, convert(varchar(8), dateadd(day, -90, getdate()), 112)) ");
        }

        sql.Append(" union all ");

        sql.Append(" select ");
        sql.Append(" activity_date = try_convert(date, cast(fd.posted_date_key as varchar(8)), 112), ");
        sql.Append(" activity_type = 'Distribution', ");
        sql.Append(" description = isnull(tt.transaction_type_name, 'Distribution'), ");
        sql.Append(" amount = -abs(isnull(fd.distributed_amount, 0)), ");
        sql.Append(" fund_name = isnull(df.fund_name, '') ");
        sql.Append($" from {WarehouseTables.FactDistribution} fd ");
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = fd.fund_key ");
        sql.Append($" inner join {WarehouseTables.DimTransactionType} tt on tt.transaction_type_key = fd.transaction_type_key ");
        sql.Append(" and isnull(tt.is_current, 1) = 1 ");
        sql.Append(" where isnull(fd.distributed_amount, 0) <> 0 ");

        if (periodFilter == "quarterly")
        {
            sql.Append(" and fd.posted_date_key >= convert(int, convert(varchar(8), dateadd(quarter, -4, getdate()), 112)) ");
        }
        else if (periodFilter == "daily")
        {
            sql.Append(" and fd.posted_date_key >= convert(int, convert(varchar(8), dateadd(day, -90, getdate()), 112)) ");
        }
    }

    private async Task<DashboardSectionDataDto> BuildInvestmentsKpiSummaryAsync(
        TimeGranularity view,
        DashboardSectionRegistry.SectionDefinition definition)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            select
                fund_count = (
                    select count(distinct f.fund_key)
                    from dbo.dim_fund f
                    where (
                        isnull(f.is_current, 1) = 1
                        or (
                            f.is_current is null
                            and getdate() between f.valid_from
                                and isnull(f.valid_to, cast('9999-12-31' as datetime2))
                        )
                    )
                ),
                total_commitments = isnull((
                    select sum(isnull(fc.committed_amount, 0))
                    from dbo.fact_commitment fc
                ), 0),
                total_deployed = isnull((
                    select sum(isnull(fi.invested_amount, 0))
                    from dbo.fact_investment fi
                ), 0),
                investor_count = (
                    select count(distinct fc.investor_key)
                    from dbo.fact_commitment fc
                )
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return BuildSectionData(definition, view, kpis: []);
        }

        return BuildSectionData(
            definition,
            view,
            kpis:
            [
                Kpi("totalFunds", "Total Funds", reader.GetInt32OrDefault("fund_count"), FieldDataTypes.Integer, caption: "Active"),
                Kpi("totalCommitments", "Total Commitments", reader.GetDecimalOrDefault("total_commitments"), FieldDataTypes.Money, caption: "Cumulative"),
                Kpi("totalDeployed", "Capital Deployed", reader.GetDecimalOrDefault("total_deployed"), FieldDataTypes.Money, caption: "Invested"),
                Kpi("totalInvestors", "Investors", reader.GetInt32OrDefault("investor_count"), FieldDataTypes.Integer, caption: "LP Count")
            ]);
    }

    private async Task<DashboardSectionDataDto> BuildAssetsKpiSummaryAsync(
        TimeGranularity view,
        DashboardSectionRegistry.SectionDefinition definition)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            select
                asset_count = (
                    select count(distinct p.property_key)
                    from dbo.dim_property p
                    where isnull(p.is_current, 1) = 1
                ),
                portfolio_count = (
                    select count(distinct p.property_key)
                    from dbo.dim_property p
                    where isnull(p.is_current, 1) = 1
                      and isnull(p.portfolio, 0) = 1
                )
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return BuildSectionData(definition, view, kpis: []);
        }

        return BuildSectionData(
            definition,
            view,
            kpis:
            [
                Kpi("totalAssets", "Total Assets", reader.GetInt32OrDefault("asset_count"), FieldDataTypes.Integer, caption: "Properties"),
                Kpi("portfolioAssets", "Portfolio Assets", reader.GetInt32OrDefault("portfolio_count"), FieldDataTypes.Integer, caption: "Portfolios")
            ]);
    }

    private static DashboardKpiCardDto Kpi(
        string key,
        string label,
        object? value,
        string formatType,
        object? change = null,
        string? changeFormatType = null,
        string? caption = null) =>
        new()
        {
            Key = key,
            Label = label,
            Value = value,
            FormatType = formatType,
            Change = change,
            ChangeFormatType = changeFormatType,
            Caption = caption
        };

    private static DynamicFieldDto Field(string key, TypedValueDto value) =>
        DisplayFieldBuilder.ToDynamicField(key, value);

    private static DashboardSectionGroupDto Group(string title, IReadOnlyList<DynamicFieldDto> fields) =>
        new() { Title = title, Fields = fields };

    private static string ToFieldKey(string label) =>
        string.Concat(label
            .Split([' ', '-', '/', '&'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
}
