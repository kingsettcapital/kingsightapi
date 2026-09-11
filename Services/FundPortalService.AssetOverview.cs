using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class FundPortalService
{
    public async Task<FundAssetOverviewDto?> GetFundAssetOverviewAsync(int fundKey)
    {
        try
        {
            return await GetFundAssetOverviewInternalAsync(fundKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get asset overview for fund {FundKey} cancelled", fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset overview for fund {FundKey}", fundKey);
            throw;
        }
    }

    private async Task<FundAssetOverviewDto?> GetFundAssetOverviewInternalAsync(int fundKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" f.fund_key, ");
        sql.Append(" isnull(c.fund, '') as fund, ");
        sql.Append(" sum(isnull(metrics.gross_leasable_area_sqft, 0)) as gla_sf, ");
        sql.Append(" sum(isnull(metrics.occupied_area_sqft, 0)) as occupied_sf, ");
        sql.Append(" sum(isnull(metrics.committed_area_sqft, 0)) as committed_sf, ");
        sql.Append(" sum(isnull(metrics.vacant_area_sqft, 0)) as vacant_sf ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        sql.Append(" inner join ( ");
        sql.Append(" select distinct property_key, consolidated_asset_key ");
        sql.Append($" from {WarehouseTables.DimOwnershipHierarchy} ");
        sql.Append(" ) e on p.property_key = e.property_key ");
        sql.Append($" inner join {WarehouseTables.DimProperty} c ");
        sql.Append(" on e.consolidated_asset_key = c.property_key ");
        sql.Append($" inner join {WarehouseTables.DimFund} f ");
        sql.Append(" on isnull(c.fund, '') = isnull(f.yardi_fund_code, '') ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" outer apply ( ");
        sql.Append(" select top 1 ");
        sql.Append(" date_key, ");
        sql.Append(" gross_leasable_area_sqft = sum(isnull(gross_leasable_area_sqft, 0)), ");
        sql.Append(" occupied_area_sqft = sum(isnull(occupied_area_sqft, 0)), ");
        sql.Append(" committed_area_sqft = sum(isnull(committed_area_sqft, 0) + isnull(occupied_area_sqft, 0)), ");
        sql.Append(" vacant_area_sqft = sum(isnull(vacant_area_sqft, 0)), ");
        sql.Append(" total_units = sum(isnull(total_units, 0)), ");
        sql.Append(" occupied_units = sum(isnull(occupied_units, 0)), ");
        sql.Append(" vacant_units = sum(isnull(vacant_units, 0)), ");
        sql.Append(" weighted_avg_lease_term_months = 0, ");
        sql.Append(" weighted_avg_lease_term_rent_months = 0 ");
        sql.Append($" from {WarehouseTables.FactAssetMetrics} m ");
        sql.Append(" where m.property_key = p.property_key ");
        sql.Append(" group by date_key ");
        sql.Append(" order by date_key desc ");
        sql.Append(" ) metrics ");
        sql.Append(" where f.fund_key = @fundKey ");
        sql.Append(" group by ");
        sql.Append(" f.fund_key, ");
        sql.Append(" isnull(c.fund, '') ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@fundKey", fundKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new FundAssetOverviewDto
        {
            FundKey = reader.GetInt32OrDefault("fund_key"),
            Fund = reader.GetStringOrEmpty("fund"),
            GlaSf = reader.GetDecimalOrDefault("gla_sf"),
            OccupiedSf = reader.GetDecimalOrDefault("occupied_sf"),
            CommittedSf = reader.GetDecimalOrDefault("committed_sf"),
            VacantSf = reader.GetDecimalOrDefault("vacant_sf"),
        };
    }
}
