using System.Text;
using System.Text.RegularExpressions;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

/// <summary>Distinct dropdown values for Kingsight portal list pages.</summary>
public sealed class PortalFilterService : IPortalFilterService
{
    private static readonly Regex QuarterTokenRegex = new(@"\bQ([1-4])\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _connectionString;
    private readonly ILogger<PortalFilterService> _logger;

    public PortalFilterService(IConfiguration configuration, ILogger<PortalFilterService> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        _logger = logger;
        _logger.LogInformation(
            "PortalFilterService ready. {ConnectionInfo}",
            ConnectionLogging.Sanitize(_connectionString));
    }

    public async Task<InvestorListFilterOptionsDto> GetInvestorListFilterOptionsAsync()
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var dimInvestor = WarehouseTables.DimInvestor;

            var investorTypes = await ReadDistinctOptionsAsync(
                connection,
                $"""
                select distinct isnull(investor_type_name, '') as option_value
                from {dimInvestor}
                where isnull(is_current, 1) = 1
                  and isnull(investor_type_name, '') <> ''
                order by option_value
                """);

            var relationships = await ReadDistinctOptionsAsync(
                connection,
                $"""
                select distinct isnull(relationship_name, '') as option_value
                from {dimInvestor}
                where isnull(is_current, 1) = 1
                  and isnull(relationship_name, '') <> ''
                order by option_value
                """);

            var quarterlyPeriods = await ReadQuarterlyPeriodOptionsAsync(connection);

            return new InvestorListFilterOptionsDto
            {
                InvestorTypes = investorTypes,
                Relationships = relationships,
                CalendarYears = BuildCalendarYearOptions(quarterlyPeriods),
                QuarterlyPeriods = quarterlyPeriods
            };
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogConnectionError(
                _logger, ex, nameof(GetInvestorListFilterOptionsAsync), _connectionString);
            throw;
        }
    }

    public async Task<FundListFilterOptionsDto> GetFundListFilterOptionsAsync()
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var fundTypes = await ReadDistinctOptionsAsync(
                connection,
                BuildCurrentFundDistinctSql("fund_type_name"));

            var strategies = await ReadDistinctOptionsAsync(
                connection,
                BuildCurrentFundDistinctSql("fund_strategy_name"));

            var quarterlyPeriods = await ReadQuarterlyPeriodOptionsAsync(connection);

            return new FundListFilterOptionsDto
            {
                FundTypes = fundTypes,
                Strategies = strategies,
                CalendarYears = BuildCalendarYearOptions(quarterlyPeriods),
                QuarterlyPeriods = quarterlyPeriods
            };
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogConnectionError(
                _logger, ex, nameof(GetFundListFilterOptionsAsync), _connectionString);
            throw;
        }
    }

    public async Task<AssetListFilterOptionsDto> GetAssetListFilterOptionsAsync()
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var assetTypes = await ReadDistinctOptionsAsync(
                connection,
                BuildPropertyDistinctSql("asset_type"));

            var investmentTypes = await ReadDistinctOptionsAsync(
                connection,
                BuildPropertyDistinctSql("investment_type"));

            var geographies = await ReadDistinctOptionsAsync(
                connection,
                BuildPropertyDistinctSql("geography"));

            var statuses = await ReadDistinctOptionsAsync(
                connection,
                BuildPropertyDistinctSql("property_status"));

            var quarterlyPeriods = await ReadAssetQuarterlyPeriodOptionsAsync(connection);

            return new AssetListFilterOptionsDto
            {
                AssetTypes = assetTypes,
                InvestmentTypes = investmentTypes,
                Geographies = geographies,
                Statuses = statuses,
                QuarterlyPeriods = quarterlyPeriods
            };
        }
        catch (Exception ex)
        {
            ConnectionLogging.LogConnectionError(
                _logger, ex, nameof(GetAssetListFilterOptionsAsync), _connectionString);
            throw;
        }
    }

    private static string BuildCurrentFundDistinctSql(string columnName)
    {
        var sql = new StringBuilder();
        sql.Append($" select distinct isnull(f.{columnName}, '') as option_value ");
        sql.Append($" from {WarehouseTables.DimFund} f ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append($" and isnull(f.{columnName}, '') <> '' ");
        sql.Append(" order by option_value ");
        return sql.ToString();
    }

    private static string BuildPropertyDistinctSql(string columnName)
    {
        var sql = new StringBuilder();
        sql.Append($" select distinct isnull(c.{columnName}, '') as option_value ");
        WarehouseSql.AppendConsolidatedAssetFrom(sql);
        sql.Append($" where isnull(c.{columnName}, '') <> '' ");
        sql.Append(" order by option_value ");
        return sql.ToString();
    }

    private static async Task<IReadOnlyList<PortalFilterOptionDto>> ReadDistinctOptionsAsync(
        SqlConnection connection,
        string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var options = new List<PortalFilterOptionDto>();
        while (await reader.ReadAsync())
        {
            var value = reader.GetStringOrEmpty("option_value");
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            options.Add(new PortalFilterOptionDto
            {
                Value = value,
                Label = value
            });
        }

        return options;
    }

    private static async Task<IReadOnlyList<PortalQuarterPeriodOptionDto>> ReadAssetQuarterlyPeriodOptionsAsync(
        SqlConnection connection)
    {
        var sql = $"""
            select
                d.quarter_year,
                d.calendar_year,
                date_key = max(m.date_key)
            from {WarehouseTables.FactAssetMetrics} m
            inner join {WarehouseTables.DimDate} d on d.date_key = m.date_key
            group by d.quarter_year, d.calendar_year
            order by d.calendar_year desc, d.quarter_year desc
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var options = new List<PortalQuarterPeriodOptionDto>();
        while (await reader.ReadAsync())
        {
            var quarterYear = reader.GetStringOrEmpty("quarter_year");
            var calendarYear = reader.GetInt32OrDefault("calendar_year");
            var dateKey = reader.GetInt32OrDefault("date_key");
            var quarter = ParseQuarterNumber(quarterYear, calendarYear);

            options.Add(new PortalQuarterPeriodOptionDto
            {
                DateKey = dateKey,
                CalendarYear = calendarYear,
                Quarter = quarter,
                QuarterYear = quarterYear,
                Label = BuildQuarterLabel(quarterYear, calendarYear, quarter)
            });
        }

        return options;
    }

    private static async Task<IReadOnlyList<PortalQuarterPeriodOptionDto>> ReadQuarterlyPeriodOptionsAsync(
        SqlConnection connection)
    {
        var sql = $"""
            select
                d.quarter_year,
                d.calendar_year,
                date_key = max(d.date_key)
            from {WarehouseTables.FactInvestorPortfolioQuarterly} q
            inner join {WarehouseTables.DimDate} d on {WarehouseSql.QuarterYearEquals("d.quarter_year", "q.quarter_year")}
            group by d.quarter_year, d.calendar_year
            order by d.calendar_year desc, d.quarter_year desc
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var options = new List<PortalQuarterPeriodOptionDto>();
        while (await reader.ReadAsync())
        {
            var quarterYear = reader.GetStringOrEmpty("quarter_year");
            var calendarYear = reader.GetInt32OrDefault("calendar_year");
            var dateKey = reader.GetInt32OrDefault("date_key");
            var quarter = ParseQuarterNumber(quarterYear, calendarYear);

            options.Add(new PortalQuarterPeriodOptionDto
            {
                DateKey = dateKey,
                CalendarYear = calendarYear,
                Quarter = quarter,
                QuarterYear = quarterYear,
                Label = BuildQuarterLabel(quarterYear, calendarYear, quarter)
            });
        }

        return options;
    }

    private static IReadOnlyList<PortalFilterOptionDto> BuildCalendarYearOptions(
        IReadOnlyList<PortalQuarterPeriodOptionDto> quarterlyPeriods)
    {
        return quarterlyPeriods
            .Select(p => p.CalendarYear)
            .Where(y => y > 0)
            .Distinct()
            .OrderByDescending(y => y)
            .Select(y => new PortalFilterOptionDto
            {
                Value = y.ToString(),
                Label = y.ToString()
            })
            .ToList();
    }

    private static int ParseQuarterNumber(string quarterYear, int calendarYear)
    {
        var match = QuarterTokenRegex.Match(quarterYear);
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

    private static string BuildQuarterLabel(string quarterYear, int calendarYear, int quarter)
    {
        if (!string.IsNullOrWhiteSpace(quarterYear))
        {
            return quarterYear;
        }

        return quarter is >= 1 and <= 4 && calendarYear > 0
            ? $"Q{quarter} {calendarYear}"
            : string.Empty;
    }
}
