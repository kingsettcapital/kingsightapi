using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class InvestorPortalService
{
    public async Task<PagedResult<InvestorFundExposureDto>> GetInvestorFundExposureAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        try
        {
            return await GetInvestorFundExposureInternalAsync(investorKey, view, period, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get {View} fund exposure for investor {InvestorKey} cancelled", view, investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {View} fund exposure for investor {InvestorKey}", view, investorKey);
            throw;
        }
    }

    public async Task<PagedResult<InvestorUnderlyingAssetGridItemDto>> GetInvestorUnderlyingAssetsAsync(
        long investorKey,
        string? search,
        int page,
        int pageSize)
    {
        try
        {
            return await GetInvestorUnderlyingAssetsInternalAsync(investorKey, search, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get underlying assets for investor {InvestorKey} cancelled", investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving underlying assets for investor {InvestorKey}", investorKey);
            throw;
        }
    }

    private async Task<PagedResult<InvestorFundExposureDto>> GetInvestorFundExposureInternalAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var factTable = PortfolioFactTable(view);

        var countSql = BuildInvestorTransactionCountSql(factTable, view, period);

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" f.fund_key, ");
        pageSql.Append(" isnull(f.fund_code, '') as fund_code, ");
        pageSql.Append(" max(isnull(f.fund_name, '')) as fund_name, ");
        AppendInvestorPortfolioMetricAggregates(pageSql, "p");
        AppendInvestorPortfolioFrom(pageSql, factTable);
        AppendInvestorTransactionWhere(pageSql, view, period);
        pageSql.Append(" group by f.fund_key, f.fund_code ");
        pageSql.Append(" order by f.fund_code ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        return await ExecuteInvestorTransactionPageAsync(
            countSql,
            pageSql,
            investorKey,
            period,
            null,
            null,
            page,
            pageSize,
            static reader =>
            {
                var commitment = reader.GetDecimalOrDefault("commitment_amount");
                var netInvested = reader.GetDecimalOrDefault("net_invested_capital_amount");

                return new InvestorFundExposureDto
                {
                    FundKey = reader.GetInt32OrDefault("fund_key"),
                    FundCode = reader.GetStringOrEmpty("fund_code"),
                    FundName = reader.GetStringOrEmpty("fund_name"),
                    CommitmentAmount = commitment,
                    NetInvestedCapitalAmount = netInvested,
                    NetDistributedAmount = reader.GetDecimalOrDefault("net_distributed_amount"),
                    ReservedAmount = reader.GetDecimalOrDefault("reserved_amount"),
                    UnfundedAmount = reader.GetDecimalOrDefault("unfunded_amount"),
                    ReleasedCapitalAmount = reader.GetNullableDecimal("released_capital_amount"),
                    InvestedPercent = PortalPortfolioMetrics.ComputeInvestedPercent(commitment, netInvested)
                };
            });
    }

    /// <summary>
    /// Investor detail Underlying Assets grid:
    /// <c>shared.dim_property</c> × <c>shared.dim_fund</c>, scoped to funds in
    /// <c>investor_servicing.fact_investor_portfolio_itd</c> for the investor.
    /// </summary>
    private async Task<PagedResult<InvestorUnderlyingAssetGridItemDto>> GetInvestorUnderlyingAssetsInternalAsync(
        long investorKey,
        string? search,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) from ( ");
        countSql.Append(" select distinct ");
        countSql.Append(" p.property_name, ");
        countSql.Append(" p.city, ");
        countSql.Append(" p.province, ");
        countSql.Append(" p.geography, ");
        countSql.Append(" p.asset_type, ");
        countSql.Append(" p.asset_sub_type, ");
        countSql.Append(" p.investment_type ");
        countSql.Append($" from {WarehouseTables.DimProperty} p ");
        WarehouseSql.AppendPropertyFundCodeJoin(countSql);
        countSql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(countSql, "p");
        WarehouseSql.AppendPropertyFundLevel000Filter(countSql, "p");
        WarehouseSql.AppendInvestorFundKeyScopeFilter(countSql);
        WarehouseSql.AppendPropertyUnderlyingAssetSearchFilter(countSql);
        countSql.Append(" ) asset_rows ");

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        countCommand.Parameters.AddWithValue("@investorKey", investorKey);
        countCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var pageSql = new StringBuilder();
        pageSql.Append(" select distinct ");
        pageSql.Append(" p.property_name, ");
        pageSql.Append(" p.city, ");
        pageSql.Append(" p.province, ");
        pageSql.Append(" p.geography, ");
        pageSql.Append(" p.asset_type, ");
        pageSql.Append(" p.asset_sub_type, ");
        pageSql.Append(" p.investment_type ");
        pageSql.Append($" from {WarehouseTables.DimProperty} p ");
        WarehouseSql.AppendPropertyFundCodeJoin(pageSql);
        pageSql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(pageSql, "p");
        WarehouseSql.AppendPropertyFundLevel000Filter(pageSql, "p");
        WarehouseSql.AppendInvestorFundKeyScopeFilter(pageSql);
        WarehouseSql.AppendPropertyUnderlyingAssetSearchFilter(pageSql);
        pageSql.Append(" order by p.property_name ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        pageCommand.Parameters.AddWithValue("@investorKey", investorKey);
        pageCommand.Parameters.AddWithValue("@search", (object?)searchTerm ?? DBNull.Value);
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<InvestorUnderlyingAssetGridItemDto>();
        await using (var reader = await pageCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                items.Add(new InvestorUnderlyingAssetGridItemDto
                {
                    PropertyName = reader.GetNullableTrimmedString("property_name"),
                    City = reader.GetNullableTrimmedString("city"),
                    Province = reader.GetNullableTrimmedString("province"),
                    Geography = reader.GetNullableTrimmedString("geography"),
                    AssetType = reader.GetNullableTrimmedString("asset_type"),
                    AssetSubType = reader.GetNullableTrimmedString("asset_sub_type"),
                    InvestmentType = reader.GetNullableTrimmedString("investment_type")
                });
            }
        }

        return new PagedResult<InvestorUnderlyingAssetGridItemDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    private static async Task<InvestorDetailMetricsDto> GetInvestorPortfolioMetricsAsync(
        SqlConnection connection,
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        var portfolioTable = PortalPortfolioListSql.PortfolioTable(view);
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" count(distinct a.fund_key) as fund_count, ");
        PortalPortfolioListSql.AppendPortfolioSummaryMetricSums(sql, "a");
        sql.Append($" from {portfolioTable} a ");
        sql.Append($" inner join {WarehouseTables.DimInvestor} b on a.investor_key = b.investor_key ");
        sql.Append(" where a.investor_key = @investorKey and ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "b");
        PortalPortfolioListSql.AppendQuarterlyPeriodFilter(sql, view, period);

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@investorKey", investorKey);
        PortalPortfolioListSql.AddPeriodParameter(command, period);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return new InvestorDetailMetricsDto();
        }

        return new InvestorDetailMetricsDto
        {
            FundCount = reader.GetInt32OrDefault("fund_count"),
            TotalCommitment = reader.GetDecimalOrDefault("total_commitment"),
            NetInvestedCapital = reader.GetDecimalOrDefault("net_invested_capital"),
            NetDistributed = reader.GetDecimalOrDefault("net_distributed"),
            ReservedAmount = reader.GetDecimalOrDefault("reserved"),
            UnfundedAmount = reader.GetDecimalOrDefault("unfunded"),
            ReleasedCapitalAmount = reader.GetNullableDecimal("released_capital")
        };
    }

    private static void AppendInvestorPortfolioMetricAggregates(StringBuilder sql, string factAlias = "p")
    {
        PortalPortfolioListSql.AppendPortfolioMetricAggregates(sql, factAlias);
    }
}
