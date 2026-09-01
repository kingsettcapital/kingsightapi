using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class PropertyPortalService
{
    public async Task<AssetFinancialMetricsDto?> GetAssetFinancialMetricsAsync(long assetKey)
    {
        try
        {
            return await GetAssetFinancialMetricsInternalAsync(assetKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Get financial metrics for asset {AssetKey} cancelled",
                assetKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving financial metrics for asset {AssetKey}",
                assetKey);
            throw;
        }
    }

    private async Task<AssetFinancialMetricsDto?> GetAssetFinancialMetricsInternalAsync(long assetKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select top 1 ");
        sql.Append(" isnull(c.fund_code, '') as fund_code, ");
        sql.Append(" a.asset_key, ");
        sql.Append(" isnull(b.property_code, '') as asset_code, ");
        sql.Append(" isnull(b.property_name, '') as asset_name, ");
        sql.Append(" d.full_date as as_of_date, ");
        sql.Append(" a.asset_ks_ownership_pct, ");
        sql.Append(" a.asset_cash_at_quarter_end, ");
        sql.Append(" a.asset_total_asset_value, ");
        sql.Append(" a.asset_debt, ");
        sql.Append(" a.asset_equity, ");
        sql.Append(" a.asset_noi, ");
        sql.Append(" a.asset_ffo, ");
        sql.Append(" a.asset_ncf, ");
        sql.Append(" a.asset_capex, ");
        sql.Append(" a.asset_nav_amount, ");
        sql.Append(" a.asset_ebitda, ");
        sql.Append(" a.asset_revenue, ");
        sql.Append(" a.asset_expense, ");
        sql.Append(" a.asset_gross_market_value, ");
        sql.Append(" a.asset_gav_amount, ");
        sql.Append(" a.asset_ltv, ");
        sql.Append(" a.asset_affo, ");
        sql.Append(" a.asset_capex_pct_noi, ");
        sql.Append(" a.total_noi_growth_amount, ");
        sql.Append(" a.total_noi_growth_pct, ");
        sql.Append(" a.same_store_noi_growth_amount, ");
        sql.Append(" a.same_store_noi_growth_pct, ");
        sql.Append(" a.current_cost_amount, ");
        sql.Append(" a.cost_basis_amount, ");
        sql.Append(" a.budgeted_noi_current_year, ");
        sql.Append(" a.forecasted_noi_current_year, ");
        sql.Append(" a.budgeted_ffo, ");
        sql.Append(" a.forecasted_ffo ");
        sql.Append($" from {WarehouseTables.FactAssetFinancialKsItd} a ");
        sql.Append($" inner join {WarehouseTables.DimProperty} b on a.asset_key = b.property_key ");
        sql.Append($" inner join {WarehouseTables.DimFund} c on c.fund_key = a.fund_key ");
        sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = a.as_of_date_key ");
        sql.Append(" where a.asset_key = @propertyKey ");
        sql.Append(" order by d.full_date desc, isnull(c.fund_code, '') ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

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

        return new AssetFinancialMetricsDto
        {
            FundCode = reader.GetStringOrEmpty("fund_code"),
            AssetKey = reader.GetInt64OrDefault("asset_key"),
            AssetCode = reader.GetStringOrEmpty("asset_code"),
            AssetName = reader.GetStringOrEmpty("asset_name"),
            AsOfDate = reader.GetNullableDateTime("as_of_date"),
            AssetKsOwnershipPct = reader.GetNullableDecimal("asset_ks_ownership_pct"),
            AssetCashAtQuarterEnd = reader.GetNullableDecimal("asset_cash_at_quarter_end"),
            AssetTotalAssetValue = reader.GetNullableDecimal("asset_total_asset_value"),
            AssetDebt = reader.GetNullableDecimal("asset_debt"),
            AssetEquity = reader.GetNullableDecimal("asset_equity"),
            AssetNoi = reader.GetNullableDecimal("asset_noi"),
            AssetFfo = reader.GetNullableDecimal("asset_ffo"),
            AssetNcf = reader.GetNullableDecimal("asset_ncf"),
            AssetCapex = reader.GetNullableDecimal("asset_capex"),
            AssetNavAmount = reader.GetNullableDecimal("asset_nav_amount"),
            AssetEbitda = reader.GetNullableDecimal("asset_ebitda"),
            AssetRevenue = reader.GetNullableDecimal("asset_revenue"),
            AssetExpense = reader.GetNullableDecimal("asset_expense"),
            AssetGrossMarketValue = reader.GetNullableDecimal("asset_gross_market_value"),
            AssetGavAmount = reader.GetNullableDecimal("asset_gav_amount"),
            AssetLtv = reader.GetNullableDecimal("asset_ltv"),
            AssetAffo = reader.GetNullableDecimal("asset_affo"),
            AssetCapexPctNoi = reader.GetNullableDecimal("asset_capex_pct_noi"),
            TotalNoiGrowthAmount = reader.GetNullableDecimal("total_noi_growth_amount"),
            TotalNoiGrowthPct = reader.GetNullableDecimal("total_noi_growth_pct"),
            SameStoreNoiGrowthAmount = reader.GetNullableDecimal("same_store_noi_growth_amount"),
            SameStoreNoiGrowthPct = reader.GetNullableDecimal("same_store_noi_growth_pct"),
            CurrentCostAmount = reader.GetNullableDecimal("current_cost_amount"),
            CostBasisAmount = reader.GetNullableDecimal("cost_basis_amount"),
            BudgetedNoiCurrentYear = reader.GetNullableDecimal("budgeted_noi_current_year"),
            ForecastedNoiCurrentYear = reader.GetNullableDecimal("forecasted_noi_current_year"),
            BudgetedFfo = reader.GetNullableDecimal("budgeted_ffo"),
            ForecastedFfo = reader.GetNullableDecimal("forecasted_ffo"),
        };
    }
}
