using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public interface IPropertyPortalService
{
    Task<PortalListPageResult<PropertyListItemDto, AssetListSummaryDto>> GetPropertiesAsync(
        string? search,
        string? assetType,
        string? investmentType,
        string? geography,
        string? status,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize,
        string? fundCode);
    Task<PropertyProfileDto?> GetPropertyByKeyAsync(long propertyKey);
    Task<AssetLeasingSummaryDto?> GetPropertyLeasingSummaryAsync(long propertyKey);
    Task<IReadOnlyList<PropertyInvestmentDto>> GetPropertyInvestmentsAsync(long propertyKey);
    Task<IReadOnlyList<PropertyFundHoldingDto>> GetPropertyFundHoldingsAsync(long propertyKey);
}

public sealed partial class PropertyPortalService : IPropertyPortalService
{
    private readonly string _connectionString;
    private readonly ILogger<PropertyPortalService> _logger;

    public PropertyPortalService(IConfiguration configuration, ILogger<PropertyPortalService> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        _logger = logger;
        _logger.LogInformation(
            "PropertyPortalService ready. {ConnectionInfo}",
            ConnectionLogging.Sanitize(_connectionString));
    }

    public async Task<PortalListPageResult<PropertyListItemDto, AssetListSummaryDto>> GetPropertiesAsync(
        string? search,
        string? assetType,
        string? investmentType,
        string? geography,
        string? status,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize,
        string? fundCode)
    {
        try
        {
            return await GetPropertiesInternalAsync(
                search, assetType, investmentType, geography, status, sortBy, sortDir, page, pageSize, fundCode);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get properties cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving properties. Search={Search}, FundCode={FundCode}, Page={Page}, PageSize={PageSize}", search, fundCode, page, pageSize);
            throw;
        }
    }

    public async Task<PropertyProfileDto?> GetPropertyByKeyAsync(long propertyKey)
    {
        try
        {
            return await GetPropertyByKeyInternalAsync(propertyKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get property {PropertyKey} cancelled", propertyKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving property {PropertyKey}", propertyKey);
            throw;
        }
    }

    public async Task<AssetLeasingSummaryDto?> GetPropertyLeasingSummaryAsync(long propertyKey)
    {
        try
        {
            return await GetPropertyLeasingSummaryInternalAsync(propertyKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get leasing summary for property {PropertyKey} cancelled", propertyKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving leasing summary for property {PropertyKey}", propertyKey);
            throw;
        }
    }

    public async Task<IReadOnlyList<PropertyInvestmentDto>> GetPropertyInvestmentsAsync(long propertyKey)
    {
        try
        {
            return await GetPropertyInvestmentsInternalAsync(propertyKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investments for property {PropertyKey} cancelled", propertyKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investments for property {PropertyKey}", propertyKey);
            throw;
        }
    }

    private async Task<PortalListPageResult<PropertyListItemDto, AssetListSummaryDto>> GetPropertiesInternalAsync(
        string? search,
        string? assetType,
        string? investmentType,
        string? geography,
        string? status,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize,
        string? fundCode)
    {
        if (!PortalListSort.TryParseProperty(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var assetTypeTerm = string.IsNullOrWhiteSpace(assetType) ? null : assetType.Trim();
        var investmentTypeTerm = string.IsNullOrWhiteSpace(investmentType) ? null : investmentType.Trim();
        var geographyTerm = string.IsNullOrWhiteSpace(geography) ? null : geography.Trim();
        var statusTerm = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        var fundCodeTerm = string.IsNullOrWhiteSpace(fundCode) ? null : fundCode.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append($" from {WarehouseTables.DimProperty} p ");
        AppendPropertyListingWhere(countSql);

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddPropertyListingParameters(
            countCommand, searchTerm, assetTypeTerm, investmentTypeTerm, geographyTerm, statusTerm, fundCodeTerm);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        var summary = await GetAssetListSummaryAsync(
            connection,
            searchTerm,
            assetTypeTerm,
            investmentTypeTerm,
            geographyTerm,
            statusTerm,
            fundCodeTerm);

        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" p.property_key, ");
        sql.Append(" isnull(p.property_code, '') as property_code, ");
        sql.Append(" isnull(p.property_name, '') as property_name, ");
        sql.Append(" isnull(p.geography, '') as geography, ");
        sql.Append(" isnull(p.city, '') as city, ");
        sql.Append(" isnull(p.province, '') as province, ");
        sql.Append(" isnull(p.asset_type, '') as asset_type, ");
        sql.Append(" isnull(p.investment_type, '') as investment_type, ");
        sql.Append(" isnull(p.development_type, '') as development_type, ");
        sql.Append(" isnull(p.property_status, '') as property_status, ");
        sql.Append(" isnull(p.portfolio, 0) as portfolio, ");
        sql.Append(" metrics.gross_leasable_area_sqft as gla_sf, ");
        sql.Append(" metrics.occupied_area_sqft as occupied_sf, ");
        sql.Append(" metrics.committed_area_sqft as committed_sf, ");
        sql.Append(" metrics.vacant_area_sqft as vacant_sf ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        WarehouseSql.AppendLatestAssetMetricsApply(sql);
        AppendPropertyListingWhere(sql);
        orderBy.AppendOrderBy(sql);
        sql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddPropertyListingParameters(
            command, searchTerm, assetTypeTerm, investmentTypeTerm, geographyTerm, statusTerm, fundCodeTerm);
        command.Parameters.AddWithValue("@offset", offset);
        command.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<PropertyListItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(MapPropertyListItem(reader));
        }

        _logger.LogInformation(
            "Retrieved {Count} properties (page {Page}, total {Total}).",
            items.Count, normalizedPage, totalCount);

        return new PortalListPageResult<PropertyListItemDto, AssetListSummaryDto>
        {
            Summary = summary,
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
        };
    }

    private static async Task<AssetListSummaryDto> GetAssetListSummaryAsync(
        SqlConnection connection,
        string? search,
        string? assetType,
        string? investmentType,
        string? geography,
        string? status,
        string? fundCode)
    {
        var summarySql = new StringBuilder();
        summarySql.Append(" select ");
        summarySql.Append(" count(*) as property_count, ");
        summarySql.Append(" sum(case when lower(isnull(p.property_status, '')) = 'active' then 1 else 0 end) as active_property_count, ");
        summarySql.Append(" sum(isnull(metrics.gross_leasable_area_sqft, 0)) as total_gla_sf, ");
        summarySql.Append(" sum(isnull(metrics.committed_area_sqft, 0)) as total_committed_sf, ");
        summarySql.Append(" sum(isnull(metrics.vacant_area_sqft, 0)) as total_vacant_sf ");
        summarySql.Append($" from {WarehouseTables.DimProperty} p ");
        WarehouseSql.AppendLatestAssetMetricsApply(summarySql);
        AppendPropertyListingWhere(summarySql);

        await using var command = new SqlCommand(summarySql.ToString(), connection);
        AddPropertyListingParameters(command, search, assetType, investmentType, geography, status, fundCode);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return new AssetListSummaryDto();
        }

        return new AssetListSummaryDto
        {
            TotalProperties = reader.GetInt32OrDefault("property_count"),
            ActiveProperties = reader.GetInt32OrDefault("active_property_count"),
            TotalGlaSf = reader.GetDecimalOrDefault("total_gla_sf"),
            TotalCommittedSf = reader.GetDecimalOrDefault("total_committed_sf"),
            TotalVacantSf = reader.GetDecimalOrDefault("total_vacant_sf")
        };
    }

    private async Task<IReadOnlyList<PropertyInvestmentDto>> GetPropertyInvestmentsInternalAsync(long propertyKey)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return await GetPropertyInvestmentsInternalAsync(propertyKey, connection);
    }

    private async Task<IReadOnlyList<PropertyInvestmentDto>> GetPropertyInvestmentsInternalAsync(
        long propertyKey,
        SqlConnection connection)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" df.fund_key, ");
        sql.Append(" isnull(df.fund_name, '') as fund_name, ");
        sql.Append(" isnull(df.fund_type_name, '') as fund_type_name, ");
        sql.Append(" isnull(df.fund_strategy_name, '') as fund_strategy_name, ");
        sql.Append(" case ");
        sql.Append(" when df.dissolution_date is not null then 'Dissolved' ");
        sql.Append(" when isnull(df.is_current, 1) = 1 then 'Active' ");
        sql.Append(" else 'Inactive' ");
        sql.Append(" end as fund_status, ");
        sql.Append(" df.fund_start_date, ");
        sql.Append(" isnull(committed.invested_amount_total, 0) as invested_amount_total, ");
        sql.Append(" isnull(currentvals.invested_amount_fmv_total, 0) as total_value, ");
        sql.Append(" case ");
        sql.Append(" when abs(isnull(currentvals.return_amount_total, 0)) > 0 ");
        sql.Append(" then ((isnull(currentvals.return_amount_fmv_total, 0) - isnull(currentvals.return_amount_total, 0)) / abs(currentvals.return_amount_total)) * 100.0 ");
        sql.Append(" else null ");
        sql.Append(" end as total_return_percent ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        WarehouseSql.AppendPropertyFundJoin(sql, "p", "df");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "df");
        sql.Append(" outer apply ( ");
        sql.Append(" select sum(isnull(fc.committed_amount, 0)) as invested_amount_total ");
        sql.Append($" from {WarehouseTables.FactCommitted} fc where fc.fund_key = df.fund_key ");
        sql.Append(" ) committed ");
        sql.Append(" outer apply ( ");
        sql.Append(" select ");
        sql.Append(" case ");
        sql.Append(" when lower(isnull(df.fund_type_name, '')) = 'unitized' then sum(isnull(fi.invested_units, 0)) ");
        sql.Append(" else sum(isnull(fi.invested_amount, 0)) ");
        sql.Append(" end as invested_amount_fmv_total, ");
        sql.Append(" sum(isnull(fi.invested_amount, 0)) as return_amount_total, ");
        sql.Append(" sum(isnull(fi.invested_amount_fmv, 0)) as return_amount_fmv_total ");
        sql.Append($" from {WarehouseTables.FactInvestment} fi where fi.fund_key = df.fund_key ");
        sql.Append(" ) currentvals ");
        sql.Append(" where p.property_key = @propertyKey ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        sql.Append(" order by df.fund_name ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@propertyKey", propertyKey);

        var items = new List<PropertyInvestmentDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var fundKey = reader.GetInt32OrDefault("fund_key");
            var fundName = reader.GetStringOrEmpty("fund_name");
            var fundType = reader.GetStringOrEmpty("fund_type_name");
            var fundStrategy = reader.GetStringOrEmpty("fund_strategy_name");
            var status = reader.GetStringOrEmpty("fund_status");
            var fundStartDate = reader.GetNullableDateTime("fund_start_date");
            var totalValue = reader.GetDecimalOrDefault("total_value");
            var totalReturnPercent = reader.GetNullableDecimal("total_return_percent");

            items.Add(new PropertyInvestmentDto
            {
                FundKey = fundKey,
                FundName = fundName,
                FundType = fundType,
                FundStrategy = fundStrategy,
                Status = status,
                FundStartDate = fundStartDate,
                TotalValue = totalValue,
                TotalReturnPercent = totalReturnPercent
            });
        }

        return items;
    }

    private static void AppendPropertyListingWhere(StringBuilder sql)
    {
        sql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        WarehouseSql.AppendPropertyFundLevel000Filter(sql, "p");
        WarehouseSql.AppendPropertyAssetTypePresentFilter(sql, "p");
        WarehouseSql.AppendPropertySearchFilter(sql, "p");
        WarehouseSql.AppendPropertyAssetTypeFilter(sql, "p");
        WarehouseSql.AppendPropertyInvestmentTypeFilter(sql, "p");
        WarehouseSql.AppendPropertyGeographyFilter(sql, "p");
        WarehouseSql.AppendPropertyStatusFilter(sql, "p");
        WarehouseSql.AppendFundCodeSearchFilter(sql, "p");
    }

    private static void AddPropertyListingParameters(
        SqlCommand command,
        string? search,
        string? assetType,
        string? investmentType,
        string? geography,
        string? status,
        string? fundCode)
    {
        command.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);
        command.Parameters.AddWithValue("@assetType", (object?)assetType ?? DBNull.Value);
        command.Parameters.AddWithValue("@investmentType", (object?)investmentType ?? DBNull.Value);
        command.Parameters.AddWithValue("@geography", (object?)geography ?? DBNull.Value);
        command.Parameters.AddWithValue("@status", (object?)status ?? DBNull.Value);
        command.Parameters.AddWithValue("@fund_code", (object?)fundCode ?? DBNull.Value);
    }

    private static PropertyListItemDto MapPropertyListItem(SqlDataReader reader)
    {
        var geography = reader.GetStringOrEmpty("geography");
        if (string.IsNullOrWhiteSpace(geography))
        {
            var city = reader.GetStringOrEmpty("city");
            var province = reader.GetStringOrEmpty("province");
            geography = string.Join(", ", new[] { city, province }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        return new PropertyListItemDto
        {
            PropertyKey = reader.GetInt64OrDefault("property_key"),
            PropertyCode = reader.GetStringOrEmpty("property_code"),
            PropertyName = reader.GetStringOrEmpty("property_name"),
            Geography = geography,
            City = reader.GetStringOrEmpty("city"),
            Province = reader.GetStringOrEmpty("province"),
            AssetType = reader.GetStringOrEmpty("asset_type"),
            InvestmentType = reader.GetStringOrEmpty("investment_type"),
            DevelopmentType = reader.GetStringOrEmpty("development_type"),
            PropertyStatus = reader.GetStringOrEmpty("property_status"),
            GlaSf = reader.GetNullableDecimal("gla_sf"),
            OccupiedSf = reader.GetNullableDecimal("occupied_sf"),
            CommittedSf = reader.GetNullableDecimal("committed_sf"),
            VacantSf = reader.GetNullableDecimal("vacant_sf"),
            IsPortfolio = reader.GetBooleanFromColumns("portfolio")
        };
    }
}
