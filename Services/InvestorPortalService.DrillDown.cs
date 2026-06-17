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

    public async Task<PagedResult<InvestorUnderlyingAssetDto>> GetInvestorAssetsAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        try
        {
            return await GetInvestorAssetsInternalAsync(investorKey, view, period, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get assets for investor {InvestorKey} cancelled", investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets for investor {InvestorKey}", investorKey);
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

    private async Task<PagedResult<InvestorUnderlyingAssetDto>> GetInvestorAssetsInternalAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) ");
        countSql.Append($" from {WarehouseTables.DimProperty} p ");
        WarehouseSql.AppendPropertyFundJoin(countSql, "p", "f");
        countSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(countSql, "f");
        AppendInvestorPropertyScopeWhere(countSql, "p", "f");

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        countCommand.Parameters.AddWithValue("@investorKey", investorKey);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" p.property_key, ");
        pageSql.Append(" isnull(p.property_name, '') as property_name, ");
        pageSql.Append(" isnull(p.asset_type, '') as asset_type, ");
        pageSql.Append(" isnull(p.city, '') as city, ");
        pageSql.Append(" isnull(f.fund_code, '') as fund_code, ");
        pageSql.Append(" isnull(f.fund_name, '') as fund_name, ");
        pageSql.Append(" metrics.gross_leasable_area_sqft as gla_sf, ");
        pageSql.Append(" metrics.occupied_area_sqft as occupied_sf, ");
        pageSql.Append(" isnull(p.property_status, '') as property_status ");
        pageSql.Append($" from {WarehouseTables.DimProperty} p ");
        WarehouseSql.AppendPropertyFundJoin(pageSql, "p", "f");
        pageSql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(pageSql, "f");
        WarehouseSql.AppendLatestAssetMetricsApply(pageSql, "p");
        AppendInvestorPropertyScopeWhere(pageSql, "p", "f");
        pageSql.Append(" order by p.property_name ");
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        pageCommand.Parameters.AddWithValue("@investorKey", investorKey);
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<InvestorUnderlyingAssetDto>();
        await using (var reader = await pageCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var glaSf = reader.GetNullableDecimal("gla_sf");
                var occupiedSf = reader.GetNullableDecimal("occupied_sf");
                decimal? occupancyPct = null;
                if (glaSf is > 0m && occupiedSf.HasValue)
                {
                    occupancyPct = Math.Round(occupiedSf.Value / glaSf.Value * 100m, 4, MidpointRounding.AwayFromZero);
                }

                items.Add(new InvestorUnderlyingAssetDto
                {
                    PropertyKey = reader.GetInt64OrDefault("property_key"),
                    PropertyName = reader.GetStringOrEmpty("property_name"),
                    AssetType = reader.GetStringOrEmpty("asset_type"),
                    City = reader.GetStringOrEmpty("city"),
                    FundCode = reader.GetStringOrEmpty("fund_code"),
                    FundName = reader.GetStringOrEmpty("fund_name"),
                    GlaSf = glaSf,
                    OccupancyPct = occupancyPct,
                    MarketValue = null,
                    CapRate = null,
                    Status = reader.GetStringOrEmpty("property_status")
                });
            }
        }

        return new PagedResult<InvestorUnderlyingAssetDto>
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

    private static void AppendInvestorPropertyScopeWhere(StringBuilder sql, string propertyAlias, string fundAlias)
    {
        sql.Append(" where ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, propertyAlias);
        WarehouseSql.AppendPropertyBelongsToFundFilter(sql, propertyAlias, fundAlias);
        WarehouseSql.AppendPropertyFundLevel000Filter(sql, propertyAlias);
        sql.Append($" and exists ( ");
        sql.Append(" select 1 from ( ");
        sql.Append($" select distinct fund_key from {WarehouseTables.FactInvestorPortfolioLtd} ");
        sql.Append(" where investor_key = @investorKey ");
        sql.Append(" union ");
        sql.Append($" select distinct fund_key from {WarehouseTables.FactCommitted} ");
        sql.Append(" where investor_key = @investorKey ");
        sql.Append(" union ");
        sql.Append($" select distinct fund_key from {WarehouseTables.FactInvestment} ");
        sql.Append(" where investor_key = @investorKey ");
        sql.Append(" ) inv_funds where inv_funds.fund_key = ");
        sql.Append(fundAlias);
        sql.Append(".fund_key ");
        sql.Append(" ) ");
    }
}
