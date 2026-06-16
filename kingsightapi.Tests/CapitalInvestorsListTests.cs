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
            Unfunded = 500000000m,
            ReleasedCapital = 120000000m
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(summary));
        var root = document.RootElement;

        Assert.Equal(500000000m, root.GetProperty("unfunded").GetDecimal());
        Assert.Equal(120000000m, root.GetProperty("released_capital").GetDecimal());
    }
}
