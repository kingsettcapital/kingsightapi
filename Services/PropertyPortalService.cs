using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public interface IPropertyPortalService
{
    Task<PagedResult<PropertyListItemDto>> GetPropertiesAsync(string? search, int page, int pageSize);
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
    }

    public async Task<PagedResult<PropertyListItemDto>> GetPropertiesAsync(string? search, int page, int pageSize)
    {
        try
        {
            return await GetPropertiesInternalAsync(search, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get properties cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving properties. Search={Search}, Page={Page}, PageSize={PageSize}", search, page, pageSize);
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

    private async Task<PagedResult<PropertyListItemDto>> GetPropertiesInternalAsync(string? search, int page, int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append($" from {WarehouseTables.DimProperty} p ");
        countSql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(countSql, "p");
        WarehouseSql.AppendPropertySearchFilter(countSql, "p");

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        countCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var sql = new StringBuilder();
        sql.Append(" select p.* ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        WarehouseSql.AppendPropertySearchFilter(sql, "p");
        sql.Append(" order by p.property_name ");
        sql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
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

        return new PagedResult<PropertyListItemDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private async Task<PropertyDetailDto?> GetPropertyByKeyInternalAsync(long propertyKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select p.* ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
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

        var fields = DisplayFieldBuilder.DictionaryFromSqlReader(reader);
        await reader.CloseAsync();

        var investments = await GetPropertyInvestmentsInternalAsync(propertyKey, connection);
        fields["investmentsCount"] = DisplayFieldBuilder.Integer(investments.Count);
        fields["status"] = DisplayFieldBuilder.Status(fields.TryGetValue("propertyStatus", out var propertyStatus)
            ? propertyStatus.Value?.ToString()
            : "Active");

        var location = DisplayFieldBuilder.Text(
            $"{GetOrDefault(fields, "city").Value}, {GetOrDefault(fields, "province").Value}".Trim(' ', ','));
        var summary = new PropertySummaryDto
        {
            PropertyKey = ToInt64(GetOrDefault(fields, "propertyKey").Value),
            PropertyName = Convert.ToString(GetOrDefault(fields, "propertyName").Value) ?? string.Empty,
            Location = Convert.ToString(location.Value) ?? string.Empty,
            AssetType = Convert.ToString(GetOrDefault(fields, "assetType").Value) ?? string.Empty,
            Status = Convert.ToString(GetOrDefault(fields, "status").Value) ?? string.Empty,
            CurrentValue = ToDecimal(GetOrDefault(fields, "currentValue", "marketValue", "propertyValue").Value),
            Yield = ToNullableDecimal(GetOrDefault(fields, "annualYieldPercent", "yieldPercent", "annualYield").Value),
            AcquisitionDate = GetOrDefault(fields, "propertyAcquisition").Value,
            Investments = ToInt32(GetOrDefault(fields, "investmentsCount").Value)
        };

        var assetDetails = new List<DynamicFieldDto>
        {
            DisplayFieldBuilder.ToDynamicField("assetType", GetOrDefault(fields, "assetType")),
            DisplayFieldBuilder.ToDynamicField("status", GetOrDefault(fields, "status")),
            DisplayFieldBuilder.ToDynamicField("location", location),
            DisplayFieldBuilder.ToDynamicField("acquisitionDate", GetOrDefault(fields, "propertyAcquisition"))
        };

        var financialInformation = new List<DynamicFieldDto>
        {
            DisplayFieldBuilder.ToDynamicField("currentValue", GetOrDefault(fields, "currentValue", "marketValue", "propertyValue")),
            DisplayFieldBuilder.ToDynamicField("annualYield", GetOrDefault(fields, "annualYieldPercent", "yieldPercent", "annualYield")),
            DisplayFieldBuilder.ToDynamicField("annualIncome", GetOrDefault(fields, "annualIncome")),
            DisplayFieldBuilder.ToDynamicField("holdingPeriod", GetOrDefault(fields, "holdingPeriod")),
            DisplayFieldBuilder.ToDynamicField("investments", GetOrDefault(fields, "investmentsCount"))
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
                    Title = "Financial Information",
                    Fields = financialInformation
                }
            ]
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
        sql.Append(" isnull(currentvals.invested_amount_fmv_total, 0) as invested_amount_fmv_total, ");
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
        sql.Append(" sum(case when lower(isnull(df.fund_type_name, '')) = 'unitized' then isnull(fi.invested_units, 0) else isnull(fi.invested_amount, 0) end) as invested_amount_fmv_total, ");
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
            var totalValue = reader.GetDecimalOrDefault("invested_amount_total");
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

    private static PropertyListItemDto MapPropertyListItem(SqlDataReader reader)
    {
        var propertyName = reader.GetStringFromColumns("property_name");
        var city = reader.GetStringFromColumns("city");
        var province = reader.GetStringFromColumns("province");
        var assetType = reader.GetStringFromColumns("asset_type");
        var status = reader.MapPropertyStatus();
        var currentValue = reader.GetDecimalFromColumns("current_value", "market_value", "property_value");
        var yieldPercent = reader.GetNullableDecimalFromColumns("annual_yield_percent", "yield_percent", "annual_yield");

        return new PropertyListItemDto
        {
            PropertyKey = reader.GetInt64FromColumns("property_key"),
            PropertyName = propertyName,
            City = city,
            Province = province,
            AssetType = assetType,
            Status = status,
            CurrentValue = currentValue,
            YieldPercent = yieldPercent
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
}
