using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class PropertyPortalService
{
    private async Task<PropertyProfileDto?> GetPropertyByKeyInternalAsync(long propertyKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" c.property_key, ");
        sql.Append(" isnull(c.property_code, '') as property_code, ");
        sql.Append(" isnull(c.property_name, '') as property_name, ");
        sql.Append(" isnull(c.geography, '') as geography, ");
        sql.Append(" isnull(c.city, '') as city, ");
        sql.Append(" isnull(c.province, '') as province, ");
        sql.Append(" isnull(c.asset_type, '') as asset_type, ");
        sql.Append(" isnull(c.investment_type, '') as investment_type, ");
        sql.Append(" isnull(c.development_type, '') as development_type, ");
        sql.Append(" isnull(c.property_status, '') as property_status, ");
        sql.Append(" isnull(c.portfolio, 0) as portfolio, ");
        sql.Append(" sum(metrics.gross_leasable_area_sqft) as gross_leasable_area_sqft, ");
        sql.Append(" sum(metrics.occupied_area_sqft) as occupied_area_sqft, ");
        sql.Append(" sum(metrics.committed_area_sqft) as committed_area_sqft, ");
        sql.Append(" sum(metrics.vacant_area_sqft) as vacant_area_sqft ");
        WarehouseSql.AppendConsolidatedAssetFrom(sql);
        WarehouseSql.AppendLatestAssetMetricsApply(sql, "p", "metrics");
        sql.Append(" where c.property_key = @propertyKey ");
        WarehouseSql.AppendConsolidatedAssetGroupBy(sql);

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

        var propertyKeyValue = reader.GetInt64FromColumns("property_key");
        var propertyCode = reader.GetStringFromColumns("property_code");
        var propertyName = reader.GetStringFromColumns("property_name");
        var city = reader.GetStringFromColumns("city");
        var province = reader.GetStringFromColumns("province");
        var geography = ResolvePropertyGeography(reader, city, province);
        var assetType = reader.GetStringFromColumns("asset_type");
        var investmentType = reader.GetStringFromColumns("investment_type");
        var developmentType = reader.GetStringFromColumns("development_type");
        var status = reader.GetStringFromColumns("property_status", "property_status_name", "status");
        var isPortfolio = reader.GetBooleanFromColumns("portfolio");
        var totalGlaSf = reader.GetNullableDecimal("gross_leasable_area_sqft");
        var occupiedAreaSf = reader.GetNullableDecimal("occupied_area_sqft");
        var committedAreaSf = reader.GetNullableDecimal("committed_area_sqft");
        var vacantAreaSf = reader.GetNullableDecimal("vacant_area_sqft");

        await reader.DisposeAsync();

        var investments = await GetPropertyInvestmentsInternalAsync(propertyKey, connection);
        var (occupancyRate, vacancyRate) = ComputeAreaRates(totalGlaSf, committedAreaSf, vacantAreaSf);

        return new PropertyProfileDto
        {
            PropertyKey = propertyKeyValue,
            PropertyCode = propertyCode,
            PropertyName = propertyName,
            Geography = geography,
            City = city,
            Province = province,
            AssetType = assetType,
            InvestmentType = investmentType,
            DevelopmentType = developmentType,
            Status = status,
            IsPortfolio = isPortfolio,
            AcquisitionDate = null,
            TotalGlaSf = totalGlaSf,
            CommittedAreaSf = committedAreaSf,
            VacantAreaSf = vacantAreaSf,
            OccupiedAreaSf = occupiedAreaSf,
            OccupancyRate = occupancyRate,
            VacancyRate = vacancyRate,
            EstMarketValue = null,
            EstAnnualNoi = null,
            InvestmentCount = investments.Count
        };
    }

    private static string ResolvePropertyGeography(SqlDataReader reader, string city, string province)
    {
        var geography = reader.GetStringOrEmpty("geography");
        if (!string.IsNullOrWhiteSpace(geography))
        {
            return geography;
        }

        return string.Join(", ", new[] { city, province }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    internal static (decimal? OccupancyRate, decimal? VacancyRate) ComputeAreaRates(
        decimal? totalGlaSf,
        decimal? committedAreaSf,
        decimal? vacantAreaSf)
    {
        if (!totalGlaSf.HasValue || totalGlaSf.Value <= 0m)
        {
            return (null, null);
        }

        decimal? occupancyRate = committedAreaSf.HasValue
            ? committedAreaSf.Value / totalGlaSf.Value * 100m
            : null;
        decimal? vacancyRate = vacantAreaSf.HasValue
            ? vacantAreaSf.Value / totalGlaSf.Value * 100m
            : null;

        return (occupancyRate, vacancyRate);
    }
}
