using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class PropertyPortalService
{
    public async Task<AssetFinancialMetricsDto?> GetAssetFinancialMetricsAsync(
        long assetKey,
        TimeGranularity view,
        int? dateKey,
        string? period,
        AssetFinancialShareBasis shareBasis)
    {
        try
        {
            return await GetAssetFinancialMetricsInternalAsync(
                assetKey, view, dateKey, period, shareBasis);
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

    private async Task<AssetFinancialMetricsDto?> GetAssetFinancialMetricsInternalAsync(
        long assetKey,
        TimeGranularity view,
        int? dateKey,
        string? period,
        AssetFinancialShareBasis shareBasis)
    {
        var fn = shareBasis == AssetFinancialShareBasis.At100Pct
            ? WarehouseTables.FnAssetFinancial100Pct
            : WarehouseTables.FnAssetFinancialKs;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var periodLabel = await AssetPortalPeriodSql.ResolvePeriodLabelAsync(
            connection, period, view, dateKey);

        var sql = new StringBuilder();
        sql.Append(" select top 1 ");
        sql.Append(" isnull(fund_code, '') as fund_code, ");
        sql.Append(" asset_key, ");
        sql.Append(" isnull(asset_code, '') as asset_code, ");
        sql.Append(" isnull(asset_name, '') as asset_name, ");
        sql.Append(" as_of_date, ");
        sql.Append(" isnull(quarter_year, '') as quarter_year, ");
        sql.Append(" asset_ks_ownership_pct, ");
        sql.Append(" isnull(asset_jv_partner, '') as asset_jv_partner, ");
        sql.Append(" asset_jv_pct, ");
        sql.Append(" asset_cash_at_quarter_end, ");
        sql.Append(" asset_total_asset_value, ");
        sql.Append(" asset_debt, ");
        sql.Append(" asset_equity, ");
        sql.Append(" asset_noi, ");
        sql.Append(" asset_prior_year_same_period, ");
        sql.Append(" asset_prior_year_end_noi, ");
        sql.Append(" asset_ffo, ");
        sql.Append(" asset_capex, ");
        sql.Append(" asset_nav_amount, ");
        sql.Append(" asset_gross_market_value, ");
        sql.Append(" asset_gav_amount, ");
        sql.Append(" asset_net_income, ");
        sql.Append(" asset_ltv, ");
        sql.Append(" asset_capex_pct_noi, ");
        sql.Append(" total_noi_growth_amount, ");
        sql.Append(" total_noi_growth_pct, ");
        sql.Append(" current_cost_book_value, ");
        sql.Append(" budgeted_noi_current_year, ");
        sql.Append(" forecasted_noi_current_year ");
        sql.Append($" from {fn}(@assetKey, @period) ");
        sql.Append(" order by as_of_date desc, isnull(fund_code, '') ");

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

        return MapAssetFinancialMetrics(reader);
    }

    private static AssetFinancialMetricsDto MapAssetFinancialMetrics(SqlDataReader reader)
    {
        return new AssetFinancialMetricsDto
        {
            FundCode = reader.GetStringOrEmpty("fund_code"),
            AssetKey = reader.GetInt64OrDefault("asset_key"),
            AssetCode = reader.GetStringOrEmpty("asset_code"),
            AssetName = reader.GetStringOrEmpty("asset_name"),
            AsOfDate = reader.GetNullableDateTime("as_of_date"),
            QuarterYear = reader.GetNullableTrimmedString("quarter_year"),
            AssetKsOwnershipPct = reader.GetNullableDecimal("asset_ks_ownership_pct"),
            AssetJvPartner = reader.GetStringOrEmpty("asset_jv_partner"),
            AssetJvPct = reader.GetNullableDecimal("asset_jv_pct"),
            AssetCashAtQuarterEnd = reader.GetNullableDecimal("asset_cash_at_quarter_end"),
            AssetTotalAssetValue = reader.GetNullableDecimal("asset_total_asset_value"),
            AssetDebt = reader.GetNullableDecimal("asset_debt"),
            AssetEquity = reader.GetNullableDecimal("asset_equity"),
            AssetNoi = reader.GetNullableDecimal("asset_noi"),
            AssetPriorYearSamePeriod = reader.GetNullableDecimal("asset_prior_year_same_period"),
            AssetPriorYearEndNoi = reader.GetNullableDecimal("asset_prior_year_end_noi"),
            AssetFfo = reader.GetNullableDecimal("asset_ffo"),
            AssetNcf = null,
            AssetCapex = reader.GetNullableDecimal("asset_capex"),
            AssetNavAmount = reader.GetNullableDecimal("asset_nav_amount"),
            AssetEbitda = null,
            AssetRevenue = null,
            AssetExpense = null,
            AssetGrossMarketValue = reader.GetNullableDecimal("asset_gross_market_value"),
            AssetGavAmount = reader.GetNullableDecimal("asset_gav_amount"),
            AssetNetIncome = reader.GetNullableDecimal("asset_net_income"),
            AssetLtv = reader.GetNullableDecimal("asset_ltv"),
            AssetAffo = null,
            AssetCapexPctNoi = reader.GetNullableDecimal("asset_capex_pct_noi"),
            TotalNoiGrowthAmount = reader.GetNullableDecimal("total_noi_growth_amount"),
            TotalNoiGrowthPct = reader.GetNullableDecimal("total_noi_growth_pct"),
            SameStoreNoiGrowthAmount = null,
            SameStoreNoiGrowthPct = null,
            CurrentCostAmount = reader.GetNullableDecimal("current_cost_book_value"),
            CostBasisAmount = null,
            BudgetedNoiCurrentYear = reader.GetNullableDecimal("budgeted_noi_current_year"),
            ForecastedNoiCurrentYear = reader.GetNullableDecimal("forecasted_noi_current_year"),
            BudgetedFfo = null,
            ForecastedFfo = null,
        };
    }
}
