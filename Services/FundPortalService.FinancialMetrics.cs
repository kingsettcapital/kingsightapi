using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class FundPortalService
{
    public async Task<FundFinancialMetricsDto?> GetFundFinancialMetricsAsync(
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        try
        {
            return await GetFundFinancialMetricsInternalAsync(fundKey, view, period);
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
        FundPeriodFilter? period)
    {
        var useQuarterly = view == TimeGranularity.Quarterly;
        var factTable = useQuarterly
            ? WarehouseTables.FactFundFinancialQuarterly
            : WarehouseTables.FactFundFinancialItd;

        var sql = new StringBuilder();
        sql.Append(" select top 1 ");
        sql.Append(" f.fund_key, ");
        sql.Append(" isnull(df.fund_code, '') as fund_code, ");
        sql.Append(" d.full_date as as_of_date, ");
        if (useQuarterly)
        {
            sql.Append(" isnull(f.quarter_year, '') as quarter_year, ");
        }
        else
        {
            sql.Append(" cast(null as varchar(32)) as quarter_year, ");
        }
        sql.Append(" f.fund_cash_at_quarter_end, ");
        sql.Append(" f.fund_total_asset_value, ");
        sql.Append(" f.fund_debt, ");
        sql.Append(" f.fund_equity, ");
        sql.Append(" f.fund_noi, ");
        sql.Append(" f.fund_ffo, ");
        sql.Append(" f.fund_ncf, ");
        sql.Append(" f.fund_capex, ");
        sql.Append(" f.fund_nav_amount, ");
        sql.Append(" f.fund_ebitda, ");
        sql.Append(" f.fund_revenue, ");
        sql.Append(" f.fund_expense, ");
        sql.Append(" f.fund_gross_market_value, ");
        sql.Append(" f.fund_gav_amount, ");
        sql.Append(" f.fund_ltv, ");
        sql.Append(" f.jv_partners_count, ");
        sql.Append(" f.jv_investments_amount, ");
        sql.Append(" f.jv_investments_pct_of_gav, ");
        sql.Append(" f.asset_held_count, ");
        sql.Append(" f.property_held_count ");
        sql.Append($" from {factTable} f ");
        sql.Append($" inner join {WarehouseTables.DimFund} df on df.fund_key = f.fund_key ");
        sql.Append($" inner join {WarehouseTables.DimDate} d on d.date_key = f.as_of_date_key ");
        sql.Append(" where f.fund_key = @fundKey ");
        if (useQuarterly && period?.DateKey is int dateKey)
        {
            sql.Append(" and f.as_of_date_key = @dateKey ");
        }
        sql.Append(" order by d.full_date desc ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@fundKey", fundKey);
        if (useQuarterly && period?.DateKey is int filterDateKey)
        {
            command.Parameters.AddWithValue("@dateKey", filterDateKey);
        }

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
            QuarterYear = reader.GetNullableString("quarter_year"),
            FundCashAtQuarterEnd = reader.GetNullableDecimal("fund_cash_at_quarter_end"),
            FundTotalAssetValue = reader.GetNullableDecimal("fund_total_asset_value"),
            FundDebt = reader.GetNullableDecimal("fund_debt"),
            FundEquity = reader.GetNullableDecimal("fund_equity"),
            FundNoi = reader.GetNullableDecimal("fund_noi"),
            FundFfo = reader.GetNullableDecimal("fund_ffo"),
            FundNcf = reader.GetNullableDecimal("fund_ncf"),
            FundCapex = reader.GetNullableDecimal("fund_capex"),
            FundNavAmount = reader.GetNullableDecimal("fund_nav_amount"),
            FundEbitda = reader.GetNullableDecimal("fund_ebitda"),
            FundRevenue = reader.GetNullableDecimal("fund_revenue"),
            FundExpense = reader.GetNullableDecimal("fund_expense"),
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
