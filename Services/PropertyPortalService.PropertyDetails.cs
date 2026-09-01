using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class PropertyPortalService
{
    public async Task<IReadOnlyList<AssetPropertyDetailRowDto>> GetPropertyDetailsAsync(long consolidatedPropertyKey)
    {
        try
        {
            return await GetPropertyDetailsInternalAsync(consolidatedPropertyKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Get property details for consolidated asset {PropertyKey} cancelled",
                consolidatedPropertyKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving property details for consolidated asset {PropertyKey}",
                consolidatedPropertyKey);
            throw;
        }
    }

    private async Task<IReadOnlyList<AssetPropertyDetailRowDto>> GetPropertyDetailsInternalAsync(
        long consolidatedPropertyKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" isnull(a.property_code, '') as property_code, ");
        sql.Append(" isnull(a.property_name, '') as property_name, ");
        sql.Append(" a.asset_to_share_pct, ");
        sql.Append(" isnull(b.asset_type, '') as asset_type, ");
        sql.Append(" isnull(b.investment_type, '') as investment_type, ");
        sql.Append(" isnull(b.development_type, '') as development_type, ");
        sql.Append(" c.gross_leasable_area_sqft, ");
        sql.Append(" committed_area_sqft = (isnull(c.occupied_area_sqft, 0) + isnull(c.committed_area_sqft, 0)), ");
        sql.Append(" c.vacant_area_sqft, ");
        sql.Append(" occupancy_rate = case ");
        sql.Append(" when isnull(c.gross_leasable_area_sqft, 0) > 0 ");
        sql.Append(" then (isnull(c.occupied_area_sqft, 0) + isnull(c.committed_area_sqft, 0)) ");
        sql.Append(" / c.gross_leasable_area_sqft * 100.00 ");
        sql.Append(" else null end, ");
        sql.Append(" vacancy_rate = case ");
        sql.Append(" when isnull(c.gross_leasable_area_sqft, 0) > 0 ");
        sql.Append(" then c.vacant_area_sqft / c.gross_leasable_area_sqft * 100.00 ");
        sql.Append(" else null end ");
        sql.Append($" from {WarehouseTables.DimOwnershipHierarchy} a ");
        sql.Append($" inner join {WarehouseTables.DimProperty} b on a.property_key = b.property_key ");
        sql.Append($" inner join {WarehouseTables.FactAssetMetrics} c on b.property_key = c.property_key ");
        sql.Append(" where a.consolidated_asset_key = @propertyKey ");
        sql.Append(" and a.property_code is not null ");
        sql.Append(" and a.asset_code is not null ");
        sql.Append(" order by a.property_name, a.property_code ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@propertyKey", consolidatedPropertyKey);

        var items = new List<AssetPropertyDetailRowDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new AssetPropertyDetailRowDto
            {
                PropertyCode = reader.GetStringOrEmpty("property_code"),
                PropertyName = reader.GetStringOrEmpty("property_name"),
                AssetToSharePct = reader.GetNullableDecimal("asset_to_share_pct"),
                AssetType = reader.GetStringOrEmpty("asset_type"),
                InvestmentType = reader.GetStringOrEmpty("investment_type"),
                DevelopmentType = reader.GetStringOrEmpty("development_type"),
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
