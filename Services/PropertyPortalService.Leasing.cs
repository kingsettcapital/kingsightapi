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
        sql.Append(" c.property_key, ");
        sql.Append(" max(metrics.date_key) as date_key, ");
        sql.Append(" sum(metrics.gross_leasable_area_sqft) as gross_leasable_area_sqft, ");
        sql.Append(" sum(metrics.occupied_area_sqft) as occupied_area_sqft, ");
        sql.Append(" sum(metrics.committed_area_sqft) as committed_area_sqft, ");
        sql.Append(" sum(metrics.vacant_area_sqft) as vacant_area_sqft, ");
        sql.Append(" sum(metrics.total_units) as total_units, ");
        sql.Append(" sum(metrics.occupied_units) as occupied_units, ");
        sql.Append(" sum(metrics.vacant_units) as vacant_units, ");
        sql.Append(" cast(0 as decimal(18, 4)) as weighted_avg_lease_term_months, ");
        sql.Append(" cast(0 as decimal(18, 4)) as weighted_avg_lease_term_rent_months, ");
        sql.Append(" sum(metrics.gla_available_to_lease_sqft) as gla_available_to_lease_sqft, ");
        sql.Append(" sum(metrics.total_leasing_committed_sqft) as total_leasing_committed_sqft, ");
        sql.Append(" sum(metrics.new_leasing_committed_sqft) as new_leasing_committed_sqft, ");
        sql.Append(" sum(metrics.renewal_leasing_committed_sqft) as renewal_leasing_committed_sqft, ");
        sql.Append(" sum(metrics.gla_available_to_lease_units) as gla_available_to_lease_units, ");
        sql.Append(" sum(metrics.total_leasing_committed_units) as total_leasing_committed_units, ");
        sql.Append(" sum(metrics.new_leasing_committed_units) as new_leasing_committed_units, ");
        sql.Append(" sum(metrics.renewal_leasing_committed_units) as renewal_leasing_committed_units, ");
        sql.Append(" cast(null as datetime2) as last_refreshed_date ");
        WarehouseSql.AppendConsolidatedAssetFrom(sql);
        WarehouseSql.AppendLatestAssetMetricsApply(sql, "p", "metrics", includeLeasingColumns: true);
        sql.Append(" where c.property_key = @propertyKey ");
        sql.Append(" group by c.property_key ");

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
