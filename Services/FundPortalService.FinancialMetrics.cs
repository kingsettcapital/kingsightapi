using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class FundPortalService
{
    public async Task<FundFinancialMetricsDto?> GetFundFinancialMetricsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? periodLabel = null)
    {
        try
        {
            return await GetFundFinancialMetricsInternalAsync(fundKey, view, period, periodLabel);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Get {View} financial metrics for fund {FundKey} cancelled",
                view,
                fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving {View} financial metrics for fund {FundKey}",
                view,
                fundKey);
            throw;
        }
    }

    private async Task<FundFinancialMetricsDto?> GetFundFinancialMetricsInternalAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? periodLabel)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var resolvedPeriod = await AssetPortalPeriodSql.ResolvePeriodLabelAsync(
            connection,
            periodLabel,
            view,
            period?.DateKey);

        var sql = new StringBuilder();
        sql.Append(" select top 1 ");
        sql.Append(" fund_key, ");
        sql.Append(" isnull(fund_code, '') as fund_code, ");
        sql.Append(" as_of_date, ");
        sql.Append(" isnull(quarter_year, '') as quarter_year, ");
        sql.Append(" fund_cash_at_quarter_end, ");
        sql.Append(" fund_total_asset_value, ");
        sql.Append(" fund_debt, ");
        sql.Append(" fund_equity, ");
        sql.Append(" fund_noi, ");
        sql.Append(" fund_ffo, ");
        sql.Append(" fund_capex, ");
        sql.Append(" fund_nav_amount, ");
        sql.Append(" fund_net_income, ");
        sql.Append(" fund_gross_market_value, ");
        sql.Append(" fund_gav_amount, ");
        sql.Append(" fund_ltv, ");
        sql.Append(" jv_partners_count, ");
        sql.Append(" jv_investments_amount, ");
        sql.Append(" jv_investments_pct_of_gav, ");
        sql.Append(" asset_held_count, ");
        sql.Append(" property_held_count ");
        sql.Append($" from {WarehouseTables.FnFundFinancial}(@fundKey, @period) ");
        sql.Append(" order by as_of_date desc ");

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@fundKey", fundKey);
        command.Parameters.AddWithValue("@period", resolvedPeriod);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new FundFinancialMetricsDto
        {
            FundKey = reader.GetInt32OrDefault("fund_key"),
            FundCode = reader.GetStringOrEmpty("fund_code"),
            AsOfDate = reader.GetNullableDateTime("as_of_date"),
            QuarterYear = reader.GetNullableTrimmedString("quarter_year"),
            FundCashAtQuarterEnd = reader.GetNullableDecimal("fund_cash_at_quarter_end"),
            FundTotalAssetValue = reader.GetNullableDecimal("fund_total_asset_value"),
            FundDebt = reader.GetNullableDecimal("fund_debt"),
            FundEquity = reader.GetNullableDecimal("fund_equity"),
            FundNoi = reader.GetNullableDecimal("fund_noi"),
            FundFfo = reader.GetNullableDecimal("fund_ffo"),
            FundNcf = null,
            FundCapex = reader.GetNullableDecimal("fund_capex"),
            FundNavAmount = reader.GetNullableDecimal("fund_nav_amount"),
            FundNetIncome = reader.GetNullableDecimal("fund_net_income"),
            FundEbitda = null,
            FundRevenue = null,
            FundExpense = null,
            FundGrossMarketValue = reader.GetNullableDecimal("fund_gross_market_value"),
            FundGavAmount = reader.GetNullableDecimal("fund_gav_amount"),
            FundLtv = reader.GetNullableDecimal("fund_ltv"),
            JvPartnersCount = reader.GetNullableInt32("jv_partners_count"),
            JvInvestmentsAmount = reader.GetNullableDecimal("jv_investments_amount"),
            JvInvestmentsPctOfGav = reader.GetNullableDecimal("jv_investments_pct_of_gav"),
            AssetHeldCount = reader.GetNullableInt32("asset_held_count"),
            PropertyHeldCount = reader.GetNullableInt32("property_held_count"),
        };
    }
}
