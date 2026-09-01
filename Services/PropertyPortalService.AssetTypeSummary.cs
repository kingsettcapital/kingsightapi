using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class PropertyPortalService
{
    public async Task<IReadOnlyList<AssetTypeSummaryRowDto>> GetAssetTypeSummaryAsync(long consolidatedPropertyKey)
    {
        try
        {
            return await GetAssetTypeSummaryInternalAsync(consolidatedPropertyKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Get asset type summary for consolidated asset {PropertyKey} cancelled",
                consolidatedPropertyKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving asset type summary for consolidated asset {PropertyKey}",
                consolidatedPropertyKey);
            throw;
        }
    }

    private async Task<IReadOnlyList<AssetTypeSummaryRowDto>> GetAssetTypeSummaryInternalAsync(
        long consolidatedPropertyKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" a.consolidated_asset_key, ");
        sql.Append(" isnull(a.consolidated_asset_code, '') as consolidated_asset_code, ");
        sql.Append(" isnull(a.consolidated_asset_name, '') as consolidated_asset_name, ");
        sql.Append(" isnull(b.asset_type, '') as asset_type, ");
        sql.Append(" gross_leasable_area_sqft = sum(c.gross_leasable_area_sqft), ");
        sql.Append(" committed_area_sqft = sum(isnull(c.occupied_area_sqft, 0) + isnull(c.committed_area_sqft, 0)), ");
        sql.Append(" vacant_area_sqft = sum(c.vacant_area_sqft), ");
        sql.Append(" occupancy_rate = (sum(isnull(c.occupied_area_sqft, 0) + isnull(c.committed_area_sqft, 0)) ");
        sql.Append(" / nullif(sum(c.gross_leasable_area_sqft), 0)) * 100.00, ");
        sql.Append(" vacancy_rate = sum(c.vacant_area_sqft) / nullif(sum(c.gross_leasable_area_sqft), 0) * 100.00 ");
        sql.Append($" from {WarehouseTables.DimOwnershipHierarchy} a ");
        sql.Append($" inner join {WarehouseTables.DimProperty} b on a.property_key = b.property_key ");
        sql.Append($" inner join {WarehouseTables.FactAssetMetrics} c on b.property_key = c.property_key ");
        sql.Append(" where a.consolidated_asset_key = @propertyKey ");
        sql.Append(" and a.property_code is not null ");
        sql.Append(" and a.asset_code is not null ");
        sql.Append(" and a.consolidated_asset_code is not null ");
        sql.Append(" group by ");
        sql.Append(" a.consolidated_asset_key, ");
        sql.Append(" isnull(a.consolidated_asset_code, ''), ");
        sql.Append(" isnull(a.consolidated_asset_name, ''), ");
        sql.Append(" isnull(b.asset_type, '') ");
        sql.Append(" order by isnull(b.asset_type, '') ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@propertyKey", consolidatedPropertyKey);

        var items = new List<AssetTypeSummaryRowDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new AssetTypeSummaryRowDto
            {
                ConsolidatedAssetKey = reader.GetInt64OrDefault("consolidated_asset_key"),
                ConsolidatedAssetCode = reader.GetStringOrEmpty("consolidated_asset_code"),
                ConsolidatedAssetName = reader.GetStringOrEmpty("consolidated_asset_name"),
                AssetType = reader.GetStringOrEmpty("asset_type"),
                GrossLeasableAreaSqft = reader.GetNullableDecimal("gross_leasable_area_sqft"),
                CommittedAreaSqft = reader.GetNullableDecimal("committed_area_sqft"),
                VacantAreaSqft = reader.GetNullableDecimal("vacant_area_sqft"),
                OccupancyRate = reader.GetNullableDecimal("occupancy_rate"),
                VacancyRate = reader.GetNullableDecimal("vacancy_rate"),
            });
        }

        return items;
    }
}
