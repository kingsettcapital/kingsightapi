using System.Text;

namespace kingsightapi.Services;

/// <summary>Validated ORDER BY for portal list pages (sortBy + sortDir query params).</summary>
internal readonly struct PortalListOrderBy
{
    public PortalListOrderBy(string sqlExpression, bool descending)
    {
        SqlExpression = sqlExpression;
        Descending = descending;
    }

    public string SqlExpression { get; }
    public bool Descending { get; }

    public void AppendOrderBy(StringBuilder sql)
    {
        sql.Append(" order by ");
        sql.Append(SqlExpression);
        sql.Append(Descending ? " desc " : " asc ");
    }
}

internal static class PortalListSort
{
    private static readonly Dictionary<string, string> InvestorColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["investorName"] = "b.investor_name",
        ["investorType"] = "b.investor_type_name",
        ["relationship"] = "b.relationship_name",
        ["relationship_name"] = "b.relationship_name",
        ["fundCount"] = "count(distinct a.fund_key)",
        ["fund_count"] = "count(distinct a.fund_key)",
        ["commitmentAmount"] = "sum(isnull(a.commitment_amount, 0))",
        ["commitment_amount"] = "sum(isnull(a.commitment_amount, 0))",
        ["netInvestedCapitalAmount"] = "sum(isnull(a.net_invested_capital_amount, 0))",
        ["net_invested_capital_amount"] = "sum(isnull(a.net_invested_capital_amount, 0))",
        ["netDistributedAmount"] = "sum(isnull(a.preferred_return_amount, 0)) + sum(isnull(a.sales_gain_amount, 0)) + sum(isnull(a.excess_cash_amount, 0))",
        ["net_distributed_amount"] = "sum(isnull(a.preferred_return_amount, 0)) + sum(isnull(a.sales_gain_amount, 0)) + sum(isnull(a.excess_cash_amount, 0))",
        ["reservedAmount"] = "sum(isnull(a.reserved_amount, 0))",
        ["reserved_amount"] = "sum(isnull(a.reserved_amount, 0))",
        ["releasedCapitalAmount"] = "sum(isnull(a.released_capital_amount, 0))",
        ["released_capital_amount"] = "sum(isnull(a.released_capital_amount, 0))"
    };

    private static readonly Dictionary<string, string> FundColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fundName"] = "b.fund_name",
        ["fundType"] = "b.fund_type_name",
        ["fund_type_name"] = "b.fund_type_name",
        ["strategy"] = "b.fund_strategy_name",
        ["fund_strategy_name"] = "b.fund_strategy_name",
        ["commitmentAmount"] = "sum(isnull(a.commitment_amount, 0))",
        ["commitment_amount"] = "sum(isnull(a.commitment_amount, 0))",
        ["netInvestedCapitalAmount"] = "sum(isnull(a.net_invested_capital_amount, 0))",
        ["net_invested_capital_amount"] = "sum(isnull(a.net_invested_capital_amount, 0))",
        ["netDistributedAmount"] = "sum(isnull(a.preferred_return_amount, 0)) + sum(isnull(a.sales_gain_amount, 0)) + sum(isnull(a.excess_cash_amount, 0))",
        ["net_distributed_amount"] = "sum(isnull(a.preferred_return_amount, 0)) + sum(isnull(a.sales_gain_amount, 0)) + sum(isnull(a.excess_cash_amount, 0))",
        ["reservedAmount"] = "sum(isnull(a.reserved_amount, 0))",
        ["reserved_amount"] = "sum(isnull(a.reserved_amount, 0))",
        ["releasedCapitalAmount"] = "sum(isnull(a.released_capital_amount, 0))",
        ["released_capital_amount"] = "sum(isnull(a.released_capital_amount, 0))"
    };

    private static readonly Dictionary<string, string> PropertyColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["propertyName"] = "p.property_name",
        ["propertyCode"] = "p.property_code",
        ["property_code"] = "p.property_code",
        ["geography"] = "p.geography",
        ["assetType"] = "p.asset_type",
        ["asset_type"] = "p.asset_type",
        ["investmentType"] = "p.investment_type",
        ["investment_type"] = "p.investment_type",
        ["developmentType"] = "p.development_type",
        ["development_type"] = "p.development_type",
        ["propertyStatus"] = "p.property_status",
        ["property_status"] = "p.property_status",
        ["status"] = "p.property_status"
    };

    /// <summary>Area SF columns are 0 in API until dim_property columns are confirmed; accept sortBy but order in SQL by name.</summary>
    private static readonly HashSet<string> PropertyPlaceholderAreaSortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "glaSf",
        "gla_sf",
        "committedSf",
        "committed_sf",
        "vacantSf",
        "vacant_sf"
    };

    public static bool TryParseInvestor(
        string? sortBy,
        string? sortDir,
        out PortalListOrderBy sort,
        out string? error) =>
        TryParse(
            sortBy,
            sortDir,
            InvestorColumns,
            "investorName, investorType, relationship, fundCount, commitmentAmount, netInvestedCapitalAmount, netDistributedAmount, reservedAmount, releasedCapitalAmount",
            "b.investor_name",
            out sort,
            out error);

    public static bool TryParseFund(
        string? sortBy,
        string? sortDir,
        out PortalListOrderBy sort,
        out string? error) =>
        TryParse(
            sortBy,
            sortDir,
            FundColumns,
            "fundName, fundType, strategy, commitmentAmount, netInvestedCapitalAmount, netDistributedAmount, reservedAmount, releasedCapitalAmount",
            "b.fund_name",
            out sort,
            out error);

    public static bool TryParseProperty(
        string? sortBy,
        string? sortDir,
        out PortalListOrderBy sort,
        out string? error)
    {
        var columnKey = string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim();
        if (columnKey is not null && PropertyPlaceholderAreaSortKeys.Contains(columnKey))
        {
            if (!TryParseDirection(sortDir, out var descending, out error))
            {
                sort = default;
                return false;
            }

            // Response values are 0 until warehouse columns exist; use a real column for ORDER BY.
            sort = new PortalListOrderBy("p.property_name", descending);
            return true;
        }

        return TryParse(
            sortBy,
            sortDir,
            PropertyColumns,
            "propertyName, propertyCode, geography, assetType, investmentType, developmentType, propertyStatus, glaSf, committedSf, vacantSf",
            "p.property_name",
            out sort,
            out error);
    }

    private static bool TryParse(
        string? sortBy,
        string? sortDir,
        IReadOnlyDictionary<string, string> columns,
        string validSortByHint,
        string defaultExpression,
        out PortalListOrderBy sort,
        out string? error)
    {
        error = null;
        var columnKey = string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim();
        var sqlExpression = columnKey is null
            ? defaultExpression
            : columns.GetValueOrDefault(columnKey);

        if (sqlExpression is null)
        {
            sort = default;
            error = $"Query parameter 'sortBy' is invalid. Valid values: {validSortByHint}.";
            return false;
        }

        if (!TryParseDirection(sortDir, out var descending, out error))
        {
            sort = default;
            return false;
        }

        sort = new PortalListOrderBy(sqlExpression, descending);
        return true;
    }

    private static bool TryParseDirection(string? sortDir, out bool descending, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(sortDir))
        {
            descending = false;
            return true;
        }

        var normalized = sortDir.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "asc":
            case "ascending":
                descending = false;
                return true;
            case "desc":
            case "descending":
                descending = true;
                return true;
            default:
                descending = false;
                error = "Query parameter 'sortDir' is invalid. Valid values: asc, desc.";
                return false;
        }
    }
}
