using System.Text.Json;
using kingsightapi.Entities;
using kingsightapi.Services;
using Xunit;

namespace kingsightapi.Tests;

public sealed class CapitalInvestorsListTests
{
    [Fact]
    public void TryParseInvestor_accepts_unfunded_and_released_sort_columns()
    {
        Assert.True(PortalListSort.TryParseInvestor("unfunded_amount", "asc", out var unfunded, out var unfundedError));
        Assert.Null(unfundedError);
        Assert.Equal("sum(isnull(a.unfunded_amount, 0))", unfunded.SqlExpression);
        Assert.False(unfunded.Descending);

        Assert.True(PortalListSort.TryParseInvestor("released_capital_amount", "desc", out var released, out var releasedError));
        Assert.Null(releasedError);
        Assert.Equal("sum(isnull(a.released_capital_amount, 0))", released.SqlExpression);
        Assert.True(released.Descending);
    }

    [Fact]
    public void InvestorListItemDto_serializes_snake_case_amount_fields()
    {
        var dto = new InvestorListItemDto
        {
            InvestorKey = 123,
            InvestorName = "Example LP",
            ReservedAmount = -464430m,
            UnfundedAmount = 250000m,
            ReleasedCapitalAmount = 0m
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto));
        var root = document.RootElement;

        Assert.Equal(250000m, root.GetProperty("unfunded_amount").GetDecimal());
        Assert.Equal(0m, root.GetProperty("released_capital_amount").GetDecimal());
        Assert.Equal(-464430m, root.GetProperty("reserved_amount").GetDecimal());
    }

    [Fact]
    public void InvestorListSummaryDto_serializes_unfunded_and_released_totals()
    {
        var summary = new InvestorListSummaryDto
        {
            TotalInvestors = 644,
            Reserved = 120000000m,
            Unfunded = 500000000m,
            ReleasedCapital = 120000000m
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(summary));
        var root = document.RootElement;

        Assert.Equal(500000000m, root.GetProperty("unfunded").GetDecimal());
        Assert.Equal(120000000m, root.GetProperty("released_capital").GetDecimal());
        Assert.Equal(120000000m, root.GetProperty("reserved").GetDecimal());
        Assert.Equal(120000000m, root.GetProperty("reserved_uncalled").GetDecimal());
    }

    [Fact]
    public void InvestorSummaryDto_serializes_identity_and_capital_fields()
    {
        var summary = new InvestorSummaryDto
        {
            InvestorKey = 758,
            InvestorName = "Jona Capital Inc.",
            InvestorTypeName = "Corporation",
            RelationshipName = "Jon Love's Funds",
            ContactName = "Jon Love",
            ContactEmail = "jon@example.com",
            AddressLine1 = "123 Main Street",
            City = "Toronto",
            ProvinceCode = "ON",
            FundCount = 9,
            TotalCommitment = 1000000m,
            NetInvestedCapital = 800000m,
            ReservedAmount = 50000m,
            UnfundedAmount = 200000m,
            ReleasedCapitalAmount = 0m
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(summary));
        var root = document.RootElement;

        Assert.Equal("Jon Love's Funds", root.GetProperty("relationship_name").GetString());
        Assert.Equal("Jon Love", root.GetProperty("contact_name").GetString());
        Assert.Equal("Corporation", root.GetProperty("investor_type_name").GetString());
        Assert.Equal("jon@example.com", root.GetProperty("contact_email").GetString());
        Assert.Equal("123 Main Street", root.GetProperty("address_line1").GetString());
        Assert.Equal("ON", root.GetProperty("province_code").GetString());
        Assert.Equal(9, root.GetProperty("fund_count").GetInt32());
        Assert.Equal(50000m, root.GetProperty("reserved_amount").GetDecimal());
    }

    [Fact]
    public void InvestorInvestmentDto_serializes_subscription_metrics()
    {
        var dto = new InvestorInvestmentDto
        {
            FundKey = 12,
            FundCode = "CREIF",
            CommitmentAmount = 667660m,
            NetInvestedCapitalAmount = 673970m,
            ReservedAmount = 67660m,
            InvestedPercent = 100.9m
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto));
        var root = document.RootElement;

        Assert.Equal("CREIF", root.GetProperty("fund_code").GetString());
        Assert.Equal(67660m, root.GetProperty("reserved_amount").GetDecimal());
        Assert.Equal(100.9m, root.GetProperty("invested_percent").GetDecimal());
    }

    [Fact]
    public void FundSummaryDto_serializes_fund_overview_aliases_and_dpi()
    {
        var summary = new FundSummaryDto
        {
            FundType = "Unlisted",
            FundStrategyName = "Income",
            Netinvestedamount = 673970m,
            NetDistributed = 0m,
            ReleasedCapitalAmount = 0m,
            CurrentValue = 673970m
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(summary));
        var root = document.RootElement;

        Assert.Equal("Unlisted", root.GetProperty("fund_type_name").GetString());
        Assert.Equal("Unlisted", root.GetProperty("fund_type").GetString());
        Assert.Equal("Income", root.GetProperty("strategy").GetString());
        Assert.Equal(673970m, root.GetProperty("net_invested_capital_amount").GetDecimal());
        Assert.Equal(0m, root.GetProperty("net_distributed_amount").GetDecimal());
        Assert.Equal(0m, root.GetProperty("released_capital").GetDecimal());
        Assert.Equal(0m, root.GetProperty("dpi").GetDecimal());
        Assert.Equal(1m, root.GetProperty("tvpi").GetDecimal());
    }

    [Fact]
    public void InvestorUnderlyingAssetGridItemDto_serializes_snake_case_and_camelCase_aliases()
    {
        var dto = new InvestorUnderlyingAssetGridItemDto
        {
            PropertyName = "Example Property",
            City = "Toronto",
            Province = "ON",
            Geography = "Canada",
            AssetType = "Office",
            AssetSubType = "Class A",
            InvestmentType = "Direct"
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto));
        var root = document.RootElement;

        Assert.Equal("Example Property", root.GetProperty("property_name").GetString());
        Assert.Equal("Example Property", root.GetProperty("propertyName").GetString());
        Assert.Equal("Toronto", root.GetProperty("city").GetString());
        Assert.Equal("ON", root.GetProperty("province").GetString());
        Assert.Equal("Office", root.GetProperty("asset_type").GetString());
        Assert.Equal("Office", root.GetProperty("assetType").GetString());
        Assert.Equal("Class A", root.GetProperty("asset_sub_type").GetString());
        Assert.Equal("Direct", root.GetProperty("investment_type").GetString());
    }
}
