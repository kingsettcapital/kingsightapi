namespace kingsightapi.Entities;

using System.Text.Json.Serialization;

/// <summary>Investor row on the module list page (Investors tab).</summary>
public sealed class InvestorListItemDto
{
    [JsonPropertyName("investor_key")]
    public long InvestorKey { get; init; }

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("investor_type")]
    public string InvestorType { get; init; } = string.Empty;

    [JsonPropertyName("investor_type_name")]
    public string InvestorTypeName { get; init; } = string.Empty;

    [JsonPropertyName("relationship_name")]
    public string RelationshipName { get; init; } = string.Empty;

    [JsonPropertyName("contact_first_name")]
    public string ContactFirstName { get; init; } = string.Empty;

    [JsonPropertyName("contact_last_name")]
    public string ContactLastName { get; init; } = string.Empty;

    [JsonPropertyName("contact_email")]
    public string? ContactEmail { get; init; }

    [JsonPropertyName("contact_name")]
    public string ContactName { get; init; } = string.Empty;

    [JsonPropertyName("address_line1")]
    public string? AddressLine1 { get; init; }

    [JsonPropertyName("address_line2")]
    public string? AddressLine2 { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("province")]
    public string? Province { get; init; }

    [JsonPropertyName("province_code")]
    public string? ProvinceCode { get; init; }

    [JsonPropertyName("fund_count")]
    public int FundCount { get; init; }

    [JsonPropertyName("commitment_amount")]
    public decimal CommitmentAmount { get; init; }

    [JsonPropertyName("net_invested_capital_amount")]
    public decimal NetInvestedCapitalAmount { get; init; }

    [JsonPropertyName("net_distributed_amount")]
    public decimal NetDistributedAmount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal? ReleasedCapitalAmount { get; init; }

    /// <summary>Legacy alias for <see cref="NetInvestedCapitalAmount"/>.</summary>
    [JsonPropertyName("total_invested")]
    public decimal TotalInvested => NetInvestedCapitalAmount;
}

/// <summary>Filter dropdown values for transaction tables (fund code or investor name).</summary>
public sealed class TransactionFilterOptionsDto
{
    public IReadOnlyList<PortalFilterOptionDto> Items { get; init; } = [];
}

/// <summary>Flat investor profile header for GET /api/CapitalInvestors/{investorKey}.</summary>
public sealed class InvestorProfileDto
{
    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("investor_type")]
    public string InvestorType { get; init; } = string.Empty;

    public string Relationship { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Contact { get; init; } = string.Empty;

    [JsonPropertyName("contact_email")]
    public string ContactEmail { get; init; } = string.Empty;

    [JsonPropertyName("total_commitment")]
    public decimal TotalCommitment { get; init; }

    [JsonPropertyName("net_invested_capital")]
    public decimal NetInvestedCapital { get; init; }

    [JsonPropertyName("net_distributed")]
    public decimal NetDistributed { get; init; }

    [JsonPropertyName("reserved_uncalled")]
    public decimal ReservedUncalled { get; init; }

    [JsonPropertyName("released_capital")]
    public decimal? ReleasedCapital { get; init; }

    [JsonPropertyName("fund_count")]
    public int FundCount { get; init; }

    public IReadOnlyList<InvestorProfileFundDto> Funds { get; init; } = [];

    [JsonPropertyName("capital_deployed")]
    public decimal CapitalDeployed { get; init; }


    [JsonPropertyName("unfunded")]
    public decimal Unfunded{ get; init; }
}

public sealed class InvestorProfileFundDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;
}

/// <summary>Per-fund holding row for investor fund holdings grid.</summary>
public sealed class InvestorFundHoldingDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    public DateTime? Since { get; init; }

    public decimal Commitment { get; init; }
    public decimal Unfunded { get; init; }

    [JsonPropertyName("net_invested")]
    public decimal NetInvested { get; init; }

    public decimal Distributed { get; init; }
    public decimal Reserved { get; init; }
}

/// <summary>Investor fund holdings from latest <c>fact_investor_portfolio_ltd</c> snapshot.</summary>
public sealed class InvestorFundHoldingsResultDto
{
    [JsonPropertyName("date_key")]
    public int? DateKey { get; init; }

    public IReadOnlyList<InvestorFundHoldingDto> Items { get; init; } = [];
}

/// <summary>Investor profile header + overview tab.</summary>
public sealed class InvestorDetailDto
{
    public InvestorSummaryDto Summary { get; init; } = new();

    /// <summary>Period-scoped KPI totals for drill-down cards (refresh-safe).</summary>
    public InvestorDetailMetricsDto? Metrics { get; init; }

    public IReadOnlyList<DynamicSectionDto> Sections { get; init; } = [];
}

/// <summary>Investor-level portfolio KPIs for a view/period (matches list aggregates).</summary>
public sealed class InvestorDetailMetricsDto
{
    [JsonPropertyName("total_commitment")]
    public decimal TotalCommitment { get; init; }

    [JsonPropertyName("net_invested_capital")]
    public decimal NetInvestedCapital { get; init; }

    [JsonPropertyName("net_distributed")]
    public decimal NetDistributed { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("reserved")]
    public decimal Reserved => ReservedAmount;

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("unfunded")]
    public decimal Unfunded => UnfundedAmount;

    [JsonPropertyName("released_capital_amount")]
    public decimal? ReleasedCapitalAmount { get; init; }

    [JsonPropertyName("fund_count")]
    public int FundCount { get; init; }
}

/// <summary>Per-fund exposure row for investor drill-down Fund Exposure grid.</summary>
public sealed class InvestorFundExposureDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("commitment_amount")]
    public decimal CommitmentAmount { get; init; }

    [JsonPropertyName("net_invested_capital_amount")]
    public decimal NetInvestedCapitalAmount { get; init; }

    [JsonPropertyName("net_distributed_amount")]
    public decimal NetDistributedAmount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal? ReleasedCapitalAmount { get; init; }

    [JsonPropertyName("invested_percent")]
    public decimal? InvestedPercent { get; init; }
}

/// <summary>Underlying asset row for investor detail Underlying Assets grid (<c>dim_property.fund</c> = <c>dim_fund.fund_code</c>).</summary>
public sealed class InvestorUnderlyingAssetGridItemDto
{
    [JsonPropertyName("property_name")]
    public string? PropertyName { get; init; }

    [JsonPropertyName("propertyName")]
    public string? PropertyNameAlias => PropertyName;

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("province")]
    public string? Province { get; init; }

    [JsonPropertyName("geography")]
    public string? Geography { get; init; }

    [JsonPropertyName("asset_type")]
    public string? AssetType { get; init; }

    [JsonPropertyName("assetType")]
    public string? AssetTypeAlias => AssetType;

    [JsonPropertyName("asset_sub_type")]
    public string? AssetSubType { get; init; }

    [JsonPropertyName("assetSubType")]
    public string? AssetSubTypeAlias => AssetSubType;

    [JsonPropertyName("investment_type")]
    public string? InvestmentType { get; init; }

    [JsonPropertyName("investmentType")]
    public string? InvestmentTypeAlias => InvestmentType;
}

public sealed class InvestorSummaryDto
{
    [JsonPropertyName("investor_key")]
    public long InvestorKey { get; init; }

    [JsonPropertyName("investor_id")]
    public int InvestorId { get; init; }

    [JsonPropertyName("investor_name")]
    public string InvestorName { get; init; } = string.Empty;

    [JsonPropertyName("investor_type")]
    public string InvestorType { get; init; } = string.Empty;

    [JsonPropertyName("investor_type_name")]
    public string InvestorTypeName { get; init; } = string.Empty;

    [JsonPropertyName("relationship_name")]
    public string RelationshipName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("contact_first_name")]
    public string ContactFirstName { get; init; } = string.Empty;

    [JsonPropertyName("contact_last_name")]
    public string ContactLastName { get; init; } = string.Empty;

    [JsonPropertyName("contact_email")]
    public string? ContactEmail { get; init; }

    [JsonPropertyName("contact_name")]
    public string ContactName { get; init; } = string.Empty;

    [JsonPropertyName("address_line1")]
    public string? AddressLine1 { get; init; }

    [JsonPropertyName("address_line2")]
    public string? AddressLine2 { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("province")]
    public string? Province { get; init; }

    [JsonPropertyName("province_code")]
    public string? ProvinceCode { get; init; }

    [JsonPropertyName("fund_count")]
    public int FundCount { get; init; }

    [JsonPropertyName("total_invested")]
    public decimal TotalInvested { get; init; }

    [JsonPropertyName("total_commitment")]
    public decimal TotalCommitment { get; init; }

    [JsonPropertyName("net_invested_capital")]
    public decimal NetInvestedCapital { get; init; }

    [JsonPropertyName("net_distributed")]
    public decimal NetDistributed { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("reserved")]
    public decimal Reserved => ReservedAmount;

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("unfunded")]
    public decimal Unfunded => UnfundedAmount;

    [JsonPropertyName("released_capital_amount")]
    public decimal? ReleasedCapitalAmount { get; init; }

    [JsonPropertyName("investments_count")]
    public int InvestmentsCount { get; init; }

    [JsonPropertyName("documents_count")]
    public int DocumentsCount { get; init; }

    [JsonPropertyName("join_year")]
    public int? JoinYear { get; init; }
}

/// <summary>Investments tab on investor profile (one row per fund).</summary>
public sealed class InvestorInvestmentDto
{
    [JsonPropertyName("fund_key")]
    public int FundKey { get; init; }

    [JsonPropertyName("fund_code")]
    public string FundCode { get; init; } = string.Empty;

    [JsonPropertyName("fund_name")]
    public string FundName { get; init; } = string.Empty;

    [JsonPropertyName("fund_type")]
    public string FundType { get; init; } = string.Empty;

    [JsonPropertyName("fund_category")]
    public string FundCategory { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("commitment_amount")]
    public decimal CommitmentAmount { get; init; }

    [JsonPropertyName("net_invested_capital_amount")]
    public decimal NetInvestedCapitalAmount { get; init; }

    [JsonPropertyName("net_distributed_amount")]
    public decimal NetDistributedAmount { get; init; }

    [JsonPropertyName("reserved_amount")]
    public decimal ReservedAmount { get; init; }

    [JsonPropertyName("unfunded_amount")]
    public decimal UnfundedAmount { get; init; }

    [JsonPropertyName("released_capital_amount")]
    public decimal? ReleasedCapitalAmount { get; init; }

    [JsonPropertyName("invested_percent")]
    public decimal? InvestedPercent { get; init; }

    public decimal InvestedAmount { get; init; }
    public decimal InvestedAmountFmv { get; init; }
    public decimal? TotalReturnPercent { get; init; }
}
