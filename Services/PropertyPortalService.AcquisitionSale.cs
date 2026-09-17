using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class PropertyPortalService
{
    public async Task<AssetAcquisitionSaleDto> GetAssetAcquisitionSaleAsync(
        long assetKey,
        TimeGranularity view,
        int? dateKey,
        string? period)
    {
        try
        {
            return await GetAssetAcquisitionSaleInternalAsync(assetKey, view, dateKey, period);
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

    private async Task<AssetAcquisitionSaleDto> GetAssetAcquisitionSaleInternalAsync(
        long assetKey,
        TimeGranularity view,
        int? dateKey,
        string? period)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var periodLabel = await AssetPortalPeriodSql.ResolvePeriodLabelAsync(
            connection, period, view, dateKey);

        var acquisition = await GetAssetAcquisitionInternalAsync(connection, assetKey, periodLabel);
        var sale = await GetAssetSaleInternalAsync(connection, assetKey, periodLabel);

        return new AssetAcquisitionSaleDto
        {
            Acquisition = acquisition,
            Sale = sale,
        };
    }

    private static async Task<AssetAcquisitionDto?> GetAssetAcquisitionInternalAsync(
        SqlConnection connection,
        long assetKey,
        string periodLabel)
    {
        var sql = new StringBuilder();
        sql.Append(" select top 1 ");
        sql.Append(" fund_key, ");
        sql.Append(" isnull(fund_code, '') as fund_code, ");
        sql.Append(" isnull(fund_name, '') as fund_name, ");
        sql.Append(" asset_key, ");
        sql.Append(" isnull(asset_code, '') as asset_code, ");
        sql.Append(" isnull(asset_name, '') as asset_name, ");
        sql.Append(" acquisition_date, ");
        sql.Append(" at_acquisition_debt, ");
        sql.Append(" at_acquisition_equity, ");
        sql.Append(" at_acquisition_total_asset_value, ");
        sql.Append(" at_acquisition_purchase_costs, ");
        sql.Append(" at_acquisition_ltv ");
        sql.Append($" from {WarehouseTables.FnAssetAcquisition}(@assetKey, @period) ");
        sql.Append(" order by ");
        sql.Append(" case when at_acquisition_total_asset_value is null then 1 else 0 end, ");
        sql.Append(" acquisition_date desc, isnull(fund_code, '') ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@assetKey", assetKey);
        command.Parameters.AddWithValue("@period", periodLabel);

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
            AcquisitionDate = FormatEventDate(reader, "acquisition_date"),
            AtAcquisitionDebt = reader.GetNullableDecimal("at_acquisition_debt"),
            AtAcquisitionEquity = reader.GetNullableDecimal("at_acquisition_equity"),
            AtAcquisitionTotalAssetValue = reader.GetNullableDecimal("at_acquisition_total_asset_value"),
            AtAcquisitionPurchaseCosts = reader.GetNullableDecimal("at_acquisition_purchase_costs"),
            AtAcquisitionLtv = reader.GetNullableDecimal("at_acquisition_ltv"),
        };
    }

    private static async Task<AssetSaleDto?> GetAssetSaleInternalAsync(
        SqlConnection connection,
        long assetKey,
        string periodLabel)
    {
        var sql = new StringBuilder();
        sql.Append(" select top 1 ");
        sql.Append(" fund_key, ");
        sql.Append(" isnull(fund_code, '') as fund_code, ");
        sql.Append(" isnull(fund_name, '') as fund_name, ");
        sql.Append(" asset_key, ");
        sql.Append(" isnull(asset_code, '') as asset_code, ");
        sql.Append(" isnull(asset_name, '') as asset_name, ");
        sql.Append(" sale_date, ");
        sql.Append(" at_sale_debt, ");
        sql.Append(" at_sale_equity, ");
        sql.Append(" at_sale_total_asset_value, ");
        sql.Append(" at_sale_selling_costs, ");
        sql.Append(" at_sale_ltv, ");
        sql.Append(" at_sale_noi ");
        sql.Append($" from {WarehouseTables.FnAssetSale}(@assetKey, @period) ");
        sql.Append(" order by ");
        sql.Append(" case when at_sale_total_asset_value is null then 1 else 0 end, ");
        sql.Append(" sale_date desc, isnull(fund_code, '') ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@assetKey", assetKey);
        command.Parameters.AddWithValue("@period", periodLabel);

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
            SaleDate = FormatEventDate(reader, "sale_date"),
            AtSaleDebt = reader.GetNullableDecimal("at_sale_debt"),
            AtSaleEquity = reader.GetNullableDecimal("at_sale_equity"),
            AtSaleTotalAssetValue = reader.GetNullableDecimal("at_sale_total_asset_value"),
            AtSaleSellingCosts = reader.GetNullableDecimal("at_sale_selling_costs"),
            AtSaleLtv = reader.GetNullableDecimal("at_sale_ltv"),
            AtSaleNoi = reader.GetNullableDecimal("at_sale_noi"),
        };
    }

    /// <summary>TVF event dates arrive as yyyyMMdd int keys; serialize as that string for the SPA mapper.</summary>
    private static string? FormatEventDate(SqlDataReader reader, string column)
    {
        if (!reader.TryGetOrdinal(column, out var ordinal) || reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        if (value is int or long or short or byte)
        {
            var key = Convert.ToInt32(value);
            return key > 0 ? key.ToString("D8") : null;
        }

        if (value is DateTime dt)
        {
            return dt.ToString("yyyyMMdd");
        }

        var text = value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
