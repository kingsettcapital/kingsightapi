using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class PropertyPortalService
{
    private async Task<AssetLeasingSummaryDto?> GetPropertyLeasingSummaryInternalAsync(long propertyKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" p.property_key, ");
        sql.Append(" metrics.date_key, ");
        sql.Append(" metrics.gross_leasable_area_sqft, ");
        sql.Append(" metrics.occupied_area_sqft, ");
        sql.Append(" metrics.committed_area_sqft, ");
        sql.Append(" metrics.vacant_area_sqft, ");
        sql.Append(" metrics.total_units, ");
        sql.Append(" metrics.occupied_units, ");
        sql.Append(" metrics.vacant_units, ");
        sql.Append(" metrics.weighted_avg_lease_term_months, ");
        sql.Append(" metrics.weighted_avg_lease_term_rent_months, ");
        sql.Append(" metrics.gla_available_to_lease_sqft, ");
        sql.Append(" metrics.total_leasing_committed_sqft, ");
        sql.Append(" metrics.new_leasing_committed_sqft, ");
        sql.Append(" metrics.renewal_leasing_committed_sqft, ");
        sql.Append(" metrics.gla_available_to_lease_units, ");
        sql.Append(" metrics.total_leasing_committed_units, ");
        sql.Append(" metrics.new_leasing_committed_units, ");
        sql.Append(" metrics.renewal_leasing_committed_units, ");
        sql.Append(" metrics.last_refreshed_date ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        WarehouseSql.AppendLatestAssetMetricsApply(sql, "p", "metrics", includeLeasingColumns: true);
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

        var propertyKeyValue = reader.GetInt64OrDefault("property_key");
        var totalGlaSf = reader.GetNullableDecimal("gross_leasable_area_sqft");
        var committedAreaSf = reader.GetNullableDecimal("committed_area_sqft");
        var vacantAreaSf = reader.GetNullableDecimal("vacant_area_sqft");
        var (occupancyRate, vacancyRate) = ComputeAreaRates(totalGlaSf, committedAreaSf, vacantAreaSf);

        return new AssetLeasingSummaryDto
        {
            PropertyKey = propertyKeyValue,
            DateKey = reader.GetNullableInt32("date_key"),
            LastRefreshedDate = reader.GetNullableDateTime("last_refreshed_date"),
            GrossLeasableAreaSqft = totalGlaSf,
            OccupiedAreaSqft = reader.GetNullableDecimal("occupied_area_sqft"),
            CommittedAreaSqft = committedAreaSf,
            VacantAreaSqft = vacantAreaSf,
            TotalUnits = reader.GetNullableInt32("total_units"),
            OccupiedUnits = reader.GetNullableInt32("occupied_units"),
            VacantUnits = reader.GetNullableInt32("vacant_units"),
            WeightedAvgLeaseTermMonths = reader.GetNullableDecimal("weighted_avg_lease_term_months"),
            WeightedAvgLeaseTermRentMonths = reader.GetNullableDecimal("weighted_avg_lease_term_rent_months"),
            GlaAvailableToLeaseSqft = reader.GetNullableDecimal("gla_available_to_lease_sqft"),
            TotalLeasingCommittedSqft = reader.GetNullableDecimal("total_leasing_committed_sqft"),
            NewLeasingCommittedSqft = reader.GetNullableDecimal("new_leasing_committed_sqft"),
            RenewalLeasingCommittedSqft = reader.GetNullableDecimal("renewal_leasing_committed_sqft"),
            GlaAvailableToLeaseUnits = reader.GetNullableInt32("gla_available_to_lease_units"),
            TotalLeasingCommittedUnits = reader.GetNullableInt32("total_leasing_committed_units"),
            NewLeasingCommittedUnits = reader.GetNullableInt32("new_leasing_committed_units"),
            RenewalLeasingCommittedUnits = reader.GetNullableInt32("renewal_leasing_committed_units"),
            OccupancyRate = occupancyRate,
            VacancyRate = vacancyRate
        };
    }
}
