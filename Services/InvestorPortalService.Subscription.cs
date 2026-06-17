using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class InvestorPortalService
{
    public async Task<InvestorFundSubscriptionDetailDto?> GetInvestorFundSubscriptionAsync(
        long investorKey,
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        try
        {
            return await GetInvestorFundSubscriptionInternalAsync(investorKey, fundKey, view, period);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Get {View} fund subscription for investor {InvestorKey} fund {FundKey} cancelled",
                view,
                investorKey,
                fundKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving {View} fund subscription for investor {InvestorKey} fund {FundKey}",
                view,
                investorKey,
                fundKey);
            throw;
        }
    }

    private async Task<InvestorFundSubscriptionDetailDto?> GetInvestorFundSubscriptionInternalAsync(
        long investorKey,
        int fundKey,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        var factTable = PortfolioFactTable(view);
        var isLtd = view == TimeGranularity.Ltd;

        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" i.investor_key, ");
        sql.Append(" isnull(i.investor_name, '') as investor_name, ");
        sql.Append(" f.fund_key, ");
        sql.Append(" f.fund_id, ");
        sql.Append(" isnull(f.fund_code, '') as fund_code, ");
        sql.Append(" isnull(f.fund_name, '') as fund_name, ");
        sql.Append(" isnull(f.fund_type_name, '') as fund_type, ");
        sql.Append(" case when isnull(f.is_active, 0) = 1 then 'Active' else 'Inactive' end as fund_status, ");
        AppendInvestorPortfolioMetricAggregates(sql, "p");
        sql.Append(", ");
        sql.Append(" sum(isnull(p.capital_called_amount, 0)) as called, ");
        sql.Append(" sum(isnull(p.investment_transferred_in_amount, 0)) as transfer_in, ");
        sql.Append(" sum(isnull(p.investment_transferred_out_amount, 0)) as transfer_out, ");
        sql.Append(" sum(isnull(p.redeemed_amount, 0)) as redemption, ");
        sql.Append(" sum(isnull(p.preferred_return_amount, 0)) as preferred_return, ");
        sql.Append(" sum(isnull(p.excess_cash_amount, 0)) as cash_dist, ");
        sql.Append(" sum(isnull(p.sales_gain_amount, 0)) as gain_dist, ");
        sql.Append(" sum(isnull(p.return_of_capital_amount, 0)) as return_of_capital, ");
        AppendSubscriptionIrrColumns(sql, isLtd);
        AppendInvestorPortfolioFrom(sql, factTable);
        sql.Append(" where p.investor_key = @investorKey and f.fund_key = @fundKey ");
        AppendPortfolioPeriodFilter(sql, view, period);
        sql.Append(" group by ");
        sql.Append(" i.investor_key, i.investor_name, f.fund_key, f.fund_id, f.fund_code, f.fund_name, f.fund_type_name, f.is_active ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@investorKey", investorKey);
        command.Parameters.AddWithValue("@fundKey", fundKey);
        command.Parameters.AddWithValue("@dateKey", (object?)period?.DateKey ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var commitment = reader.GetDecimalOrDefault("commitment_amount");
        var netInvested = reader.GetDecimalOrDefault("net_invested_capital_amount");
        var netDistributed = reader.GetDecimalOrDefault("net_distributed_amount");
        var reserved = reader.GetDecimalOrDefault("reserved_amount");
        var released = reader.GetDecimalOrDefault("released_capital_amount");
        var investedPercent = ComputeInvestedPercent(commitment, netInvested);
        var performance = ComputeSubscriptionPerformance(commitment, netInvested, netDistributed, netInvested);

        return new InvestorFundSubscriptionDetailDto
        {
            Summary = new InvestorFundSubscriptionSummaryDto
            {
                InvestorKey = reader.GetInt64OrDefault("investor_key"),
                InvestorName = reader.GetStringOrEmpty("investor_name"),
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                FundType = reader.GetStringOrEmpty("fund_type"),
                FundId = reader.GetInt32OrDefault("fund_id"),
                Status = reader.GetStringOrEmpty("fund_status")
            },
            CapitalAccount = new InvestorFundCapitalAccountDto
            {
                TotalCommitment = commitment,
                NetInvestedCapital = netInvested,
                NetDistributed = netDistributed,
                ReservedUncalled = reserved,
                ReleasedCapital = released,
                InvestedPercent = investedPercent,
                TotalValue = netInvested,
                Tvpi = performance.Tvpi
            },
            Performance = performance,
            CapitalActivities = new InvestorFundCapitalActivitiesBlockDto
            {
                Called = reader.GetDecimalOrDefault("called"),
                TransferIn = reader.GetDecimalOrDefault("transfer_in"),
                TransferOut = reader.GetDecimalOrDefault("transfer_out"),
                Redemption = reader.GetDecimalOrDefault("redemption")
            },
            Distributions = new InvestorFundDistributionsBlockDto
            {
                PreferredReturn = reader.GetDecimalOrDefault("preferred_return"),
                CashDist = reader.GetDecimalOrDefault("cash_dist"),
                GainDist = reader.GetDecimalOrDefault("gain_dist"),
                ReturnOfCapital = reader.GetDecimalOrDefault("return_of_capital")
            },
            Irr = MapSubscriptionIrr(reader, isLtd)
        };
    }

    private static void AppendSubscriptionIrrColumns(StringBuilder sql, bool isLtd)
    {
        if (isLtd)
        {
            sql.Append(" cast(null as decimal(18,6)) as irr_1_year_pct, ");
            sql.Append(" cast(null as decimal(18,6)) as irr_3_year_pct, ");
            sql.Append(" cast(null as decimal(18,6)) as irr_5_year_pct, ");
            sql.Append(" cast(null as decimal(18,6)) as irr_7_year_pct, ");
            sql.Append(" cast(null as decimal(18,6)) as irr_10_year_pct, ");
            sql.Append(" cast(null as decimal(18,6)) as irr_ltd_pct ");
            return;
        }

        sql.Append(" max(p.irr_1_year_pct) as irr_1_year_pct, ");
        sql.Append(" max(p.irr_3_year_pct) as irr_3_year_pct, ");
        sql.Append(" max(p.irr_5_year_pct) as irr_5_year_pct, ");
        sql.Append(" max(p.irr_7_year_pct) as irr_7_year_pct, ");
        sql.Append(" max(p.irr_10_year_pct) as irr_10_year_pct, ");
        sql.Append(" max(p.irr_ltd_pct) as irr_ltd_pct ");
    }

    private static InvestorFundIrrBlockDto MapSubscriptionIrr(SqlDataReader reader, bool isLtd)
    {
        if (isLtd)
        {
            return new InvestorFundIrrBlockDto();
        }

        return new InvestorFundIrrBlockDto
        {
            Irr1YearPct = reader.GetNullableDecimal("irr_1_year_pct"),
            Irr3YearPct = reader.GetNullableDecimal("irr_3_year_pct"),
            Irr5YearPct = reader.GetNullableDecimal("irr_5_year_pct"),
            Irr7YearPct = reader.GetNullableDecimal("irr_7_year_pct"),
            Irr10YearPct = reader.GetNullableDecimal("irr_10_year_pct"),
            IrrLtdPct = reader.GetNullableDecimal("irr_ltd_pct")
        };
    }

    private static InvestorFundSubscriptionPerformanceDto ComputeSubscriptionPerformance(
        decimal commitment,
        decimal netInvested,
        decimal netDistributed,
        decimal totalValue)
    {
        var investedPercent = ComputeInvestedPercent(commitment, netInvested);
        if (netInvested <= 0m)
        {
            return new InvestorFundSubscriptionPerformanceDto
            {
                DeploymentPercent = investedPercent
            };
        }

        var tvpi = RoundRatio((netDistributed + totalValue) / netInvested);
        var dpi = RoundRatio(netDistributed / netInvested);
        var rvpi = RoundRatio(totalValue / netInvested);

        return new InvestorFundSubscriptionPerformanceDto
        {
            Tvpi = tvpi,
            Dpi = dpi,
            Rvpi = rvpi,
            DeploymentPercent = investedPercent
        };
    }

    private static decimal? ComputeInvestedPercent(decimal commitment, decimal netInvested) =>
        commitment > 0m
            ? Math.Round(netInvested / commitment * 100m, 1, MidpointRounding.AwayFromZero)
            : null;

    private static decimal? RoundRatio(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
