using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

/// <summary>Header global search across investors, funds, and assets by name.</summary>
public sealed class GlobalSearchService : IGlobalSearchService
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;
    private const int PerEntityFetchCap = 15;

    private readonly string _connectionString;
    private readonly ILogger<GlobalSearchService> _logger;

    public GlobalSearchService(IConfiguration configuration, ILogger<GlobalSearchService> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        _logger = logger;
    }

    public async Task<GlobalSearchResponseDto> SearchAsync(string? search, int limit)
    {
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (term is null)
        {
            return new GlobalSearchResponseDto { Search = string.Empty, Results = [] };
        }

        var resultLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var investors = await SearchInvestorsAsync(connection, term);
            var funds = await SearchFundsAsync(connection, term);
            var assets = await SearchAssetsAsync(connection, term);

            var results = investors
                .Concat(funds)
                .Concat(assets)
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Take(resultLimit)
                .ToList();

            _logger.LogInformation(
                "Global search for '{Search}' returned {Count} results.",
                term,
                results.Count);

            return new GlobalSearchResponseDto
            {
                Search = term,
                Results = results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing global search. Search={Search}", term);
            throw;
        }
    }

    private static async Task<IReadOnlyList<GlobalSearchResultDto>> SearchInvestorsAsync(
        SqlConnection connection,
        string search)
    {
        var sql = new StringBuilder();
        sql.Append(" select top (@fetchLimit) ");
        sql.Append(" i.investor_key, ");
        sql.Append(" isnull(i.investor_name, '') as investor_name, ");
        sql.Append(" isnull(i.investor_type_name, '') as investor_type_name, ");
        sql.Append(" isnull(i.relationship_name, '') as relationship_name ");
        sql.Append($" from {WarehouseTables.DimInvestor} i ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "i");
        sql.Append(" and lower(isnull(i.investor_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append(" order by i.investor_name ");

        await using var command = new SqlCommand(sql.ToString(), connection);
        command.Parameters.AddWithValue("@search", search);
        command.Parameters.AddWithValue("@fetchLimit", PerEntityFetchCap);

        var results = new List<GlobalSearchResultDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var investorType = reader.GetStringOrEmpty("investor_type_name");
            var relationship = reader.GetStringOrEmpty("relationship_name");
            results.Add(new GlobalSearchResultDto
            {
                EntityType = PortalEntityTypes.Investors,
                EntityKey = reader.GetInt64OrDefault("investor_key"),
                Name = reader.GetStringOrEmpty("investor_name"),
                Subtitle = BuildInvestorSubtitle(investorType, relationship)
            });
        }

        return results;
    }

    private static async Task<IReadOnlyList<GlobalSearchResultDto>> SearchFundsAsync(
        SqlConnection connection,
        string search)
    {
        var sql = new StringBuilder();
        sql.Append(" select top (@fetchLimit) ");
        sql.Append(" b.fund_key, ");
        sql.Append(" isnull(b.fund_name, '') as fund_name, ");
        sql.Append(" isnull(b.fund_strategy_name, '') as fund_strategy_name, ");
        sql.Append(" isnull(b.fund_type_name, '') as fund_type_name, ");
        sql.Append(" aum = isnull(sum(a.net_invested_capital_amount), 0) ");
        sql.Append($" from {WarehouseTables.DimFund} b ");
        sql.Append($" left join {WarehouseTables.FactInvestorPortfolioLtd} a on a.fund_key = b.fund_key ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentFundFilter(sql, "b");
        sql.Append(" and lower(isnull(b.fund_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append(" group by b.fund_key, b.fund_name, b.fund_strategy_name, b.fund_type_name ");
        sql.Append(" order by b.fund_name ");

        await using var command = new SqlCommand(sql.ToString(), connection);
        command.Parameters.AddWithValue("@search", search);
        command.Parameters.AddWithValue("@fetchLimit", PerEntityFetchCap);

        var results = new List<GlobalSearchResultDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var aum = reader.GetDecimalOrDefault("aum");
            var strategy = reader.GetStringOrEmpty("fund_strategy_name");
            var fundType = reader.GetStringOrEmpty("fund_type_name");
            results.Add(new GlobalSearchResultDto
            {
                EntityType = PortalEntityTypes.Investments,
                EntityKey = reader.GetInt32OrDefault("fund_key"),
                Name = reader.GetStringOrEmpty("fund_name"),
                Subtitle = BuildFundSubtitle(aum, strategy, fundType)
            });
        }

        return results;
    }

    private static async Task<IReadOnlyList<GlobalSearchResultDto>> SearchAssetsAsync(
        SqlConnection connection,
        string search)
    {
        var sql = new StringBuilder();
        sql.Append(" select top (@fetchLimit) ");
        sql.Append(" p.property_key, ");
        sql.Append(" isnull(p.property_name, '') as property_name, ");
        sql.Append(" isnull(p.property_code, '') as property_code, ");
        sql.Append(" isnull(p.geography, '') as geography, ");
        sql.Append(" isnull(p.asset_type, '') as asset_type ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        WarehouseSql.AppendPropertyFundLevel000Filter(sql, "p");
        sql.Append(" and lower(isnull(p.property_name, '')) like '%' + lower(@search) + '%' ");
        sql.Append(" order by p.property_name ");

        await using var command = new SqlCommand(sql.ToString(), connection);
        command.Parameters.AddWithValue("@search", search);
        command.Parameters.AddWithValue("@fetchLimit", PerEntityFetchCap);

        var results = new List<GlobalSearchResultDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var geography = reader.GetStringOrEmpty("geography");
            var assetType = reader.GetStringOrEmpty("asset_type");
            var propertyCode = reader.GetStringOrEmpty("property_code");
            results.Add(new GlobalSearchResultDto
            {
                EntityType = PortalEntityTypes.Assets,
                EntityKey = reader.GetInt64OrDefault("property_key"),
                Name = reader.GetStringOrEmpty("property_name"),
                Subtitle = BuildAssetSubtitle(geography, assetType, propertyCode)
            });
        }

        return results;
    }

    private static string BuildInvestorSubtitle(string investorType, string relationship)
    {
        if (!string.IsNullOrWhiteSpace(investorType) && !string.IsNullOrWhiteSpace(relationship))
        {
            return $"{investorType} · {relationship}";
        }

        return !string.IsNullOrWhiteSpace(investorType)
            ? investorType
            : relationship;
    }

    private static string BuildFundSubtitle(decimal aum, string strategy, string fundType)
    {
        if (aum > 0)
        {
            return $"AUM: {FormatCompactMoney(aum)}";
        }

        if (!string.IsNullOrWhiteSpace(strategy))
        {
            return strategy;
        }

        return fundType;
    }

    private static string BuildAssetSubtitle(string geography, string assetType, string propertyCode)
    {
        if (!string.IsNullOrWhiteSpace(geography) && !string.IsNullOrWhiteSpace(assetType))
        {
            return $"{geography} · {assetType}";
        }

        if (!string.IsNullOrWhiteSpace(geography))
        {
            return geography;
        }

        if (!string.IsNullOrWhiteSpace(assetType))
        {
            return assetType;
        }

        return propertyCode;
    }

    private static string FormatCompactMoney(decimal value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000_000m)
        {
            return $"${abs / 1_000_000_000m:0.##}B";
        }

        if (abs >= 1_000_000m)
        {
            return $"${abs / 1_000_000m:0.##}M";
        }

        if (abs >= 1_000m)
        {
            return $"${abs / 1_000m:0.##}K";
        }

        return $"${abs:0.##}";
    }
}
