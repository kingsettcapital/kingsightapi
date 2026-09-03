using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class PropertyPortalService
{
    public async Task<AssetAcquisitionSaleDto> GetAssetAcquisitionSaleAsync(long assetKey)
    {
        try
        {
            return await GetAssetAcquisitionSaleInternalAsync(assetKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Get acquisition/sale for asset {AssetKey} cancelled",
                assetKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving acquisition/sale for asset {AssetKey}",
                assetKey);
            throw;
        }
    }

    private async Task<AssetAcquisitionSaleDto> GetAssetAcquisitionSaleInternalAsync(long assetKey)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var acquisition = await GetAssetAcquisitionInternalAsync(connection, assetKey);
        var sale = await GetAssetSaleInternalAsync(connection, assetKey);

        return new AssetAcquisitionSaleDto
        {
            Acquisition = acquisition,
            Sale = sale,
        };
    }

    private static async Task<AssetAcquisitionDto?> GetAssetAcquisitionInternalAsync(
        SqlConnection connection,
        long assetKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select top 1 ");
        sql.Append(" c.fund_key, ");
        sql.Append(" isnull(c.fund_code, '') as fund_code, ");
        sql.Append(" isnull(c.fund_name, '') as fund_name, ");
        sql.Append(" a.asset_key, ");
        sql.Append(" isnull(b.property_code, '') as asset_code, ");
        sql.Append(" isnull(b.property_name, '') as asset_name, ");
        sql.Append(" a.acquisition_date_key as acquisition_date, ");
        sql.Append(" a.at_acquisition_debt, ");
        sql.Append(" a.at_acquisition_equity, ");
        sql.Append(" a.at_acquisition_total_asset_value, ");
        sql.Append(" a.at_acquisition_purchase_costs, ");
        sql.Append(" a.at_acquisition_ltv ");
        sql.Append($" from {WarehouseTables.FactAssetAcquisition} a ");
        sql.Append($" inner join {WarehouseTables.DimProperty} b on a.asset_key = b.property_key ");
        sql.Append($" inner join {WarehouseTables.DimFund} c on c.fund_key = a.fund_key ");
        sql.Append(" where a.asset_key = @propertyKey ");
        sql.Append(" order by ");
        sql.Append(" case when a.at_acquisition_total_asset_value is null then 1 else 0 end, ");
        sql.Append(" a.acquisition_date_key desc, isnull(c.fund_code, '') ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@propertyKey", assetKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new AssetAcquisitionDto
        {
            FundKey = reader.GetInt64OrDefault("fund_key"),
            FundCode = reader.GetStringOrEmpty("fund_code"),
            FundName = reader.GetStringOrEmpty("fund_name"),
            AssetKey = reader.GetInt64OrDefault("asset_key"),
            AssetCode = reader.GetStringOrEmpty("asset_code"),
            AssetName = reader.GetStringOrEmpty("asset_name"),
            AcquisitionDate = reader.GetNullableDateTimeFlexible("acquisition_date"),
            AtAcquisitionDebt = reader.GetNullableDecimal("at_acquisition_debt"),
            AtAcquisitionEquity = reader.GetNullableDecimal("at_acquisition_equity"),
            AtAcquisitionTotalAssetValue = reader.GetNullableDecimal("at_acquisition_total_asset_value"),
            AtAcquisitionPurchaseCosts = reader.GetNullableDecimal("at_acquisition_purchase_costs"),
            AtAcquisitionLtv = reader.GetNullableDecimal("at_acquisition_ltv"),
        };
    }

    private static async Task<AssetSaleDto?> GetAssetSaleInternalAsync(
        SqlConnection connection,
        long assetKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select top 1 ");
        sql.Append(" c.fund_key, ");
        sql.Append(" isnull(c.fund_code, '') as fund_code, ");
        sql.Append(" isnull(c.fund_name, '') as fund_name, ");
        sql.Append(" a.asset_key, ");
        sql.Append(" isnull(b.property_code, '') as asset_code, ");
        sql.Append(" isnull(b.property_name, '') as asset_name, ");
        sql.Append(" a.sale_date_key as sale_date, ");
        sql.Append(" a.at_sale_debt, ");
        sql.Append(" a.at_sale_equity, ");
        sql.Append(" a.at_sale_total_asset_value, ");
        sql.Append(" a.at_sale_selling_costs, ");
        sql.Append(" a.at_sale_ltv, ");
        sql.Append(" a.at_sale_noi ");
        sql.Append($" from {WarehouseTables.FactAssetSale} a ");
        sql.Append($" inner join {WarehouseTables.DimProperty} b on a.asset_key = b.property_key ");
        sql.Append($" inner join {WarehouseTables.DimFund} c on c.fund_key = a.fund_key ");
        sql.Append(" where a.asset_key = @propertyKey ");
        sql.Append(" order by ");
        sql.Append(" case when a.at_sale_total_asset_value is null then 1 else 0 end, ");
        sql.Append(" a.sale_date_key desc, isnull(c.fund_code, '') ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@propertyKey", assetKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new AssetSaleDto
        {
            FundKey = reader.GetInt64OrDefault("fund_key"),
            FundCode = reader.GetStringOrEmpty("fund_code"),
            FundName = reader.GetStringOrEmpty("fund_name"),
            AssetKey = reader.GetInt64OrDefault("asset_key"),
            AssetCode = reader.GetStringOrEmpty("asset_code"),
            AssetName = reader.GetStringOrEmpty("asset_name"),
            SaleDate = reader.GetNullableDateTimeFlexible("sale_date"),
            AtSaleDebt = reader.GetNullableDecimal("at_sale_debt"),
            AtSaleEquity = reader.GetNullableDecimal("at_sale_equity"),
            AtSaleTotalAssetValue = reader.GetNullableDecimal("at_sale_total_asset_value"),
            AtSaleSellingCosts = reader.GetNullableDecimal("at_sale_selling_costs"),
            AtSaleLtv = reader.GetNullableDecimal("at_sale_ltv"),
            AtSaleNoi = reader.GetNullableDecimal("at_sale_noi"),
        };
    }
}
