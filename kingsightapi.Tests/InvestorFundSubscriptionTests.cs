using System.Text.Json;
using kingsightapi.Entities;
using Xunit;

namespace kingsightapi.Tests;

public sealed class InvestorFundSubscriptionTests
{
    [Fact]
    public void InvestorFundSubscriptionDetailDto_serializes_expected_blocks()
    {
        var detail = new InvestorFundSubscriptionDetailDto
        {
            Summary = new InvestorFundSubscriptionSummaryDto
            {
                InvestorKey = 758,
                InvestorName = "1000146653 Ontario Limited",
                FundKey = 12,
                FundCode = "CREIF",
                FundName = "KingSett Canadian Real Estate Income Fund LP",
                FundType = "Unlisted",
                FundId = 12,
                Status = "Active"
            },
            CapitalAccount = new InvestorFundCapitalAccountDto
            {
                TotalCommitment = 667660m,
                NetInvestedCapital = 673970m,
                NetDistributed = 0m,
                ReservedUncalled = 67660m,
                ReleasedCapital = 0m,
                InvestedPercent = 100.9m,
                TotalValue = 673970m,
                Tvpi = 1.00m
            },
            Performance = new InvestorFundSubscriptionPerformanceDto
            {
                Tvpi = 1.00m,
                Dpi = 0m,
                Rvpi = 1.00m,
                DeploymentPercent = 100.9m
            },
            CapitalActivities = new InvestorFundCapitalActivitiesBlockDto
            {
                Called = 673970m,
                TransferIn = 0m,
                TransferOut = 0m,
                Redemption = 0m
            },
            Distributions = new InvestorFundDistributionsBlockDto
            {
                PreferredReturn = 0m,
                CashDist = 0m,
                GainDist = 0m,
                ReturnOfCapital = 0m
            }
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(detail, options));
        var root = document.RootElement;

        Assert.Equal(758, root.GetProperty("summary").GetProperty("investor_key").GetInt64());
        Assert.Equal(12, root.GetProperty("summary").GetProperty("fund_key").GetInt32());
        Assert.Equal(667660m, root.GetProperty("capital_account").GetProperty("total_commitment").GetDecimal());
        Assert.Equal(673970m, root.GetProperty("capital_account").GetProperty("net_invested_capital").GetDecimal());
        Assert.Equal(673970m, root.GetProperty("capital_activities").GetProperty("called").GetDecimal());
    }
}
