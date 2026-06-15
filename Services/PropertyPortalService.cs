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
    Task<PropertyDetailDto?> GetPropertyByKeyAsync(long propertyKey);
    Task<IReadOnlyList<PropertyInvestmentDto>> GetPropertyInvestmentsAsync(long propertyKey);
}

public sealed class PropertyPortalService : IPropertyPortalService
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

    public async Task<PropertyDetailDto?> GetPropertyByKeyAsync(long propertyKey)
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

    private async Task<PropertyDetailDto?> GetPropertyByKeyInternalAsync(long propertyKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select p.*, ");
        sql.Append(" metrics.gross_leasable_area_sqft, ");
        sql.Append(" metrics.occupied_area_sqft, ");
        sql.Append(" metrics.committed_area_sqft, ");
        sql.Append(" metrics.vacant_area_sqft, ");
        sql.Append(" metrics.weighted_avg_lease_term_months ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        WarehouseSql.AppendLatestAssetMetricsApply(sql);
        sql.Append(" where p.property_key = @propertyKey ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@propertyKey", propertyKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var glaSf = reader.GetNullableDecimal("gross_leasable_area_sqft");
        var occupiedSf = reader.GetNullableDecimal("occupied_area_sqft");
        var weightedAvgLeaseTermMonths = reader.GetNullableDecimal("weighted_avg_lease_term_months");

        var fields = DisplayFieldBuilder.DictionaryFromSqlReader(reader);
        await reader.CloseAsync();

        var investments = await GetPropertyInvestmentsInternalAsync(propertyKey, connection);
        fields["investmentsCount"] = DisplayFieldBuilder.Integer(investments.Count);
        fields["status"] = DisplayFieldBuilder.Status(fields.TryGetValue("propertyStatus", out var propertyStatus)
            ? propertyStatus.Value?.ToString()
            : "Active");

        var location = DisplayFieldBuilder.Text(
            $"{GetOrDefault(fields, "city").Value}, {GetOrDefault(fields, "province").Value}".Trim(' ', ','));
        // TODO: Map Ownership from warehouse query when column is available.
        const bool ownership = false;
        var assetSize = glaSf ?? 0m;
        var isPortfolio = ToBoolean(GetOrDefault(fields, "portfolio").Value);

        var summary = new PropertySummaryDto
        {
            PropertyKey = ToInt64(GetOrDefault(fields, "propertyKey").Value),
            PropertyName = Convert.ToString(GetOrDefault(fields, "propertyName").Value) ?? string.Empty,
            Location = Convert.ToString(location.Value) ?? string.Empty,
            AssetType = Convert.ToString(GetOrDefault(fields, "assetType").Value) ?? string.Empty,
            Status = Convert.ToString(GetOrDefault(fields, "status").Value) ?? string.Empty,
            Ownership = ownership,
            AssetSize = assetSize,
            IsPortfolio = isPortfolio,
            AcquisitionDate = GetOrDefault(fields, "propertyAcquisition").Value,
            Investments = ToInt32(GetOrDefault(fields, "investmentsCount").Value)
        };

        var assetDetails = new List<DynamicFieldDto>
        {
            DisplayFieldBuilder.ToDynamicField("assetType", GetOrDefault(fields, "assetType")),
            DisplayFieldBuilder.ToDynamicField("status", GetOrDefault(fields, "status")),
            DisplayFieldBuilder.ToDynamicField("location", location),
            DisplayFieldBuilder.ToDynamicField("acquisitionDate", GetOrDefault(fields, "propertyAcquisition")),
            DisplayFieldBuilder.ToDynamicField("ownership", DisplayFieldBuilder.Boolean(ownership)),
            DisplayFieldBuilder.ToDynamicField("assetSize", DisplayFieldBuilder.Number(assetSize)),
            DisplayFieldBuilder.ToDynamicField("isPortfolio", DisplayFieldBuilder.Boolean(isPortfolio))
        };

        return new PropertyDetailDto
        {
            Summary = summary,
            Sections =
            [
                new DynamicSectionDto
                {
                    Title = "Asset Details",
                    Fields = assetDetails
                },
                new DynamicSectionDto
                {
                    Title = "Acquisition",
                    Fields = BuildLifecycleMetricFields(glaSf, occupiedSf, weightedAvgLeaseTermMonths)
                },
                new DynamicSectionDto
                {
                    Title = "Sale",
                    Fields = BuildLifecycleMetricFields(glaSf, occupiedSf, weightedAvgLeaseTermMonths)
                }
            ]
        };
    }

    /// <summary>Acquisition/Sale metrics; GLA, occupancy, and WALT from <c>fact_asset_metrics</c> when present.</summary>
    private static List<DynamicFieldDto> BuildLifecycleMetricFields(
        decimal? glaSf,
        decimal? occupiedSf,
        decimal? weightedAvgLeaseTermMonths)
    {
        decimal? occupancy = glaSf > 0 && occupiedSf.HasValue
            ? occupiedSf.Value / glaSf.Value * 100m
            : null;

        return
        [
            DisplayFieldBuilder.ToDynamicField("debt", DisplayFieldBuilder.Money(0m)),
            DisplayFieldBuilder.ToDynamicField("equity", DisplayFieldBuilder.Money(0m)),
            DisplayFieldBuilder.ToDynamicField("totalAssetValue", DisplayFieldBuilder.Money(0m)),
            DisplayFieldBuilder.ToDynamicField("assetLevelDebtPercent", DisplayFieldBuilder.Percent(0m)),
            DisplayFieldBuilder.ToDynamicField("purchaseCosts", DisplayFieldBuilder.Money(0m)),
            DisplayFieldBuilder.ToDynamicField("ltv", DisplayFieldBuilder.Percent(0m)),
            DisplayFieldBuilder.ToDynamicField("capRate", DisplayFieldBuilder.Percent(0m)),
            DisplayFieldBuilder.ToDynamicField("noi", DisplayFieldBuilder.Money(0m)),
            DisplayFieldBuilder.ToDynamicField("gla", glaSf.HasValue ? DisplayFieldBuilder.Number(glaSf.Value) : DisplayFieldBuilder.Number(0m)),
            DisplayFieldBuilder.ToDynamicField("propertyManager", DisplayFieldBuilder.Number(0m)),
            DisplayFieldBuilder.ToDynamicField(
                "occupancy",
                occupancy.HasValue ? DisplayFieldBuilder.Percent(occupancy.Value) : DisplayFieldBuilder.Percent(0m)),
            DisplayFieldBuilder.ToDynamicField(
                "weightedAverageLeaseTerm",
                weightedAvgLeaseTermMonths.HasValue
                    ? DisplayFieldBuilder.Number(weightedAvgLeaseTermMonths.Value)
                    : DisplayFieldBuilder.Number(0m)),
            DisplayFieldBuilder.ToDynamicField("averageInPlaceRentPerSf", DisplayFieldBuilder.Money(0m)),
            DisplayFieldBuilder.ToDynamicField("salesPerSfRetail", DisplayFieldBuilder.Money(0m))
        ];
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

    private static TypedValueDto GetOrDefault(
        IReadOnlyDictionary<string, TypedValueDto> fields,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return DisplayFieldBuilder.Text(string.Empty);
    }

    private static int ToInt32(object? value)
    {
        if (value is null or DBNull)
        {
            return 0;
        }

        var raw = Convert.ToString(value)?.Trim();
        return int.TryParse(raw, out var parsed) ? parsed : 0;
    }

    private static long ToInt64(object? value)
    {
        if (value is null or DBNull)
        {
            return 0L;
        }

        var raw = Convert.ToString(value)?.Trim();
        return long.TryParse(raw, out var parsed) ? parsed : 0L;
    }

    private static decimal ToDecimal(object? value)
    {
        if (value is null or DBNull)
        {
            return 0m;
        }

        var raw = Convert.ToString(value)?.Trim();
        return decimal.TryParse(raw, out var parsed) ? parsed : 0m;
    }

    private static decimal? ToNullableDecimal(object? value)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        var raw = Convert.ToString(value)?.Trim();
        return decimal.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static bool ToBoolean(object? value)
    {
        if (value is null or DBNull)
        {
            return false;
        }

        return value switch
        {
            bool b => b,
            byte or sbyte or short or ushort or int or uint or long or ulong =>
                Convert.ToInt64(value) != 0,
            decimal or double or float =>
                Convert.ToDecimal(value) != 0m,
            string s when bool.TryParse(s, out var parsed) => parsed,
            string s => s.Equals("1", StringComparison.OrdinalIgnoreCase)
                || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || s.Equals("y", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
