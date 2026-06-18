using System.Text;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class InvestorPortalService : IInvestorPortalService
{
    private readonly string _connectionString;
    private readonly ILogger<InvestorPortalService> _logger;

    public InvestorPortalService(IConfiguration configuration, ILogger<InvestorPortalService> logger)
    {
        _connectionString = configuration.GetConnectionString("FabricConnectionString")
            ?? throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        _logger = logger;
        _logger.LogInformation(
            "InvestorPortalService ready. {ConnectionInfo}",
            ConnectionLogging.Sanitize(_connectionString));
    }

    public async Task<PortalListPageResult<InvestorListItemDto, InvestorListSummaryDto>> GetInvestorsAsync(
        string? search,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? investorType,
        string? relationship,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        try
        {
            return await GetInvestorsInternalAsync(
                search, view, period, investorType, relationship, sortBy, sortDir, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investors cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving investors. Search={Search}, View={View}, Page={Page}, PageSize={PageSize}",
                search,
                view,
                page,
                pageSize);
            throw;
        }
    }

    public async Task<InvestorProfileDto?> GetInvestorByKeyAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        try
        {
            return await GetInvestorByKeyInternalAsync(investorKey, view, period);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get investor {InvestorKey} cancelled", investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving investor {InvestorKey}", investorKey);
            throw;
        }
    }

    public async Task<PagedResult<InvestorInvestmentDto>> GetInvestorFundsAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        try
        {
            return await GetInvestorFundsInternalAsync(investorKey, view, period, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get funds for investor {InvestorKey} cancelled", investorKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving funds for investor {InvestorKey}", investorKey);
            throw;
        }
    }

    private async Task<PortalListPageResult<InvestorListItemDto, InvestorListSummaryDto>> GetInvestorsInternalAsync(
        string? search,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? investorType,
        string? relationship,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!PortalListSort.TryParseInvestor(sortBy, sortDir, out var orderBy, out var sortError))
        {
            throw new ArgumentException(sortError);
        }

        var (normalizedPage, normalizedPageSize, offset) = Pagination.Normalize(page, pageSize);
        var searchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var investorTypeTerm = string.IsNullOrWhiteSpace(investorType) ? null : investorType.Trim();
        var relationshipTerm = string.IsNullOrWhiteSpace(relationship) ? null : relationship.Trim();
        var portfolioTable = PortalPortfolioListSql.PortfolioTable(view);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var countSql = new StringBuilder();
        countSql.Append(" select count(*) from ( ");
        countSql.Append(" select b.investor_key ");
        AppendInvestorListingFrom(countSql, portfolioTable, view, period);
        countSql.Append(" group by b.investor_key, b.investor_name, b.investor_type_name, ");
        countSql.Append(" b.relationship_name, b.contact_first_name, b.contact_last_name ");
        countSql.Append(" ) investor_rows ");

        await using var countCommand = new SqlCommand(countSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddInvestorListingParameters(countCommand, searchTerm, investorTypeTerm, relationshipTerm, period);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        var summary = await GetInvestorListSummaryAsync(
            connection, portfolioTable, view, period, searchTerm, investorTypeTerm, relationshipTerm);

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" b.investor_key, ");
        pageSql.Append(" b.investor_name, ");
        pageSql.Append(" isnull(b.investor_type_name, '') as investor_type_name, ");
        pageSql.Append(" isnull(b.relationship_name, '') as relationship_name, ");
        pageSql.Append(" isnull(b.contact_first_name, '') as contact_first_name, ");
        pageSql.Append(" isnull(b.contact_last_name, '') as contact_last_name, ");
        AppendInvestorListIdentityColumns(pageSql);
        pageSql.Append(" count(distinct a.fund_key) as fund_count, ");
        PortalPortfolioListSql.AppendPortfolioMetricAggregates(pageSql);
        AppendInvestorListingFrom(pageSql, portfolioTable, view, period);
        pageSql.Append(" group by b.investor_key, b.investor_name, b.investor_type_name, ");
        pageSql.Append(" b.relationship_name, b.contact_first_name, b.contact_last_name ");
        orderBy.AppendOrderBy(pageSql);
        pageSql.Append(" offset @offset rows fetch next @pageSize rows only ");

        await using var pageCommand = new SqlCommand(pageSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        AddInvestorListingParameters(pageCommand, searchTerm, investorTypeTerm, relationshipTerm, period);
        pageCommand.Parameters.AddWithValue("@offset", offset);
        pageCommand.Parameters.AddWithValue("@pageSize", normalizedPageSize);

        var items = new List<InvestorListItemDto>();
        await using (var pageReader = await pageCommand.ExecuteReaderAsync())
        {
            while (await pageReader.ReadAsync())
            {
                items.Add(MapInvestorListItem(pageReader));
            }
        }

        _logger.LogInformation(
            "Retrieved {Count} investors ({View}, page {Page}, total {Total}).",
            items.Count,
            view,
            normalizedPage,
            totalCount);

        return new PortalListPageResult<InvestorListItemDto, InvestorListSummaryDto>
        {
            Summary = new InvestorListSummaryDto
            {
                TotalInvestors = totalCount,
                TotalCommitment = summary.TotalCommitment,
                NetInvestedCapital = summary.NetInvestedCapital,
                NetDistributed = summary.NetDistributed,
                Reserved = summary.Reserved,
                Unfunded = summary.Unfunded,
                ReleasedCapital = summary.ReleasedCapital
            },
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
        };
    }

    private static async Task<InvestorListSummaryDto> GetInvestorListSummaryAsync(
        SqlConnection connection,
        string portfolioTable,
        TimeGranularity view,
        FundPeriodFilter? period,
        string? search,
        string? investorType,
        string? relationship)
    {
        var summarySql = new StringBuilder();
        summarySql.Append(" select ");
        summarySql.Append(" count(distinct b.investor_key) as investor_count, ");
        PortalPortfolioListSql.AppendPortfolioSummaryMetricSums(summarySql);
        AppendInvestorListingFrom(summarySql, portfolioTable, view, period);

        await using var command = new SqlCommand(summarySql.ToString(), connection);
        AddInvestorListingParameters(command, search, investorType, relationship, period);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return new InvestorListSummaryDto();
        }

        return new InvestorListSummaryDto
        {
            TotalInvestors = reader.GetInt32OrDefault("investor_count"),
            TotalCommitment = reader.GetDecimalOrDefault("total_commitment"),
            NetInvestedCapital = reader.GetDecimalOrDefault("net_invested_capital"),
            NetDistributed = reader.GetDecimalOrDefault("net_distributed"),
            Reserved = reader.GetDecimalOrDefault("reserved"),
            Unfunded = reader.GetDecimalOrDefault("unfunded"),
            ReleasedCapital = reader.GetDecimalOrDefault("released_capital")
        };
    }

    private static void AppendInvestorListingFrom(
        StringBuilder sql,
        string portfolioTable,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        sql.Append($" from {portfolioTable} a ");
        sql.Append($" inner join {WarehouseTables.DimInvestor} b on a.investor_key = b.investor_key ");
        sql.Append(" where ");
        WarehouseSql.AppendCurrentInvestorFilter(sql, "b");
        PortalPortfolioListSql.AppendQuarterlyPeriodFilter(sql, view, period);
        WarehouseSql.AppendInvestorSearchFilter(sql, "b");
        WarehouseSql.AppendInvestorTypeFilter(sql, "b");
        WarehouseSql.AppendInvestorRelationshipFilter(sql, "b");
    }

    private static void AddInvestorListingParameters(
        SqlCommand command,
        string? search,
        string? investorType,
        string? relationship,
        FundPeriodFilter? period)
    {
        command.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);
        command.Parameters.AddWithValue("@investorType", (object?)investorType ?? DBNull.Value);
        command.Parameters.AddWithValue("@relationship", (object?)relationship ?? DBNull.Value);
        PortalPortfolioListSql.AddPeriodParameter(command, period);
    }

    private static void AppendInvestorListIdentityColumns(StringBuilder sql, string investorAlias = "b")
    {
        sql.Append($" max({investorAlias}.contact_email) as contact_email, ");
        sql.Append($" max({investorAlias}.address_line1) as address_line1, ");
        sql.Append($" max({investorAlias}.address_line2) as address_line2, ");
        sql.Append($" max({investorAlias}.city) as city, ");
        sql.Append($" max({investorAlias}.province) as province, ");
        sql.Append($" max({investorAlias}.province_code) as province_code, ");
    }

    private static InvestorListItemDto MapInvestorListItem(SqlDataReader reader)
    {
        var commitment = reader.GetDecimalOrDefault("commitment_amount");
        var netInvested = reader.GetDecimalOrDefault("net_invested_capital_amount");
        var contactFirst = reader.GetStringOrEmpty("contact_first_name");
        var contactLast = reader.GetStringOrEmpty("contact_last_name");

        return new InvestorListItemDto
        {
            InvestorKey = reader.GetInt64OrDefault("investor_key"),
            InvestorName = reader.GetStringOrEmpty("investor_name"),
            InvestorType = reader.GetStringOrEmpty("investor_type_name"),
            InvestorTypeName = reader.GetStringOrEmpty("investor_type_name"),
            RelationshipName = reader.GetStringOrEmpty("relationship_name"),
            ContactFirstName = contactFirst,
            ContactLastName = contactLast,
            ContactEmail = reader.GetNullableTrimmedString("contact_email"),
            ContactName = PortalPortfolioMetrics.FormatContactName(contactFirst, contactLast),
            AddressLine1 = reader.GetNullableTrimmedString("address_line1"),
            AddressLine2 = reader.GetNullableTrimmedString("address_line2"),
            City = reader.GetNullableTrimmedString("city"),
            Province = reader.GetNullableTrimmedString("province"),
            ProvinceCode = reader.GetNullableTrimmedString("province_code"),
            FundCount = reader.GetInt32OrDefault("fund_count"),
            CommitmentAmount = commitment,
            NetInvestedCapitalAmount = netInvested,
            NetDistributedAmount = reader.GetDecimalOrDefault("net_distributed_amount"),
            ReservedAmount = reader.GetDecimalOrDefault("reserved_amount"),
            UnfundedAmount = reader.GetDecimalOrDefault("unfunded_amount"),
            ReleasedCapitalAmount = reader.GetNullableDecimal("released_capital_amount")
        };
    }

    private async Task<InvestorProfileDto?> GetInvestorByKeyInternalAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var investorSql = new StringBuilder();
        investorSql.Append(" select ");
        investorSql.Append(" i.investor_name, ");
        investorSql.Append(" isnull(i.investor_type_name, '') as investor_type_name, ");
        investorSql.Append(" isnull(i.relationship_name, '') as relationship_name, ");
        investorSql.Append(" case when isnull(i.is_current, 1) = 1 then 'Active' else 'Inactive' end as investor_status, ");
        investorSql.Append(" isnull(i.contact_first_name, '') as contact_first_name, ");
        investorSql.Append(" isnull(i.contact_last_name, '') as contact_last_name, ");
        investorSql.Append(" isnull(i.contact_email, '') as contact_email ");
        investorSql.Append($" from {WarehouseTables.DimInvestor} i ");
        investorSql.Append(" where i.investor_key = @investorKey ");
        investorSql.Append(" and ");
        WarehouseSql.AppendCurrentInvestorFilter(investorSql, "i");

        await using var investorCommand = new SqlCommand(investorSql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        investorCommand.Parameters.AddWithValue("@investorKey", investorKey);

        await using var investorReader = await investorCommand.ExecuteReaderAsync();
        if (!await investorReader.ReadAsync())
        {
            return null;
        }

        var investorName = investorReader.GetStringOrEmpty("investor_name");
        var investorType = investorReader.GetStringOrEmpty("investor_type_name");
        var relationship = investorReader.GetStringOrEmpty("relationship_name");
        var status = investorReader.GetStringOrEmpty("investor_status");
        var contact = BuildContactDisplay(
            investorReader.GetStringOrEmpty("contact_first_name"),
            investorReader.GetStringOrEmpty("contact_last_name"),
            investorReader.GetStringOrEmpty("contact_email"));

        await investorReader.DisposeAsync();

        var metrics = await GetInvestorPortfolioMetricsAsync(connection, investorKey, view, period);
        var funds = await LoadInvestorProfileFundsAsync(connection, investorKey, view, period);
        var capitalDeployed = await LoadCapitalDeployedAsync(connection, investorKey, view, period);

        return new InvestorProfileDto
        {
            InvestorName = investorName,
            InvestorType = investorType,
            Relationship = relationship,
            Status = status,
            Contact = contact,
            TotalCommitment = metrics.TotalCommitment,
            NetInvestedCapital = metrics.NetInvestedCapital,
            NetDistributed = metrics.NetDistributed,
            ReservedUncalled = metrics.ReservedAmount,
            ReleasedCapital = metrics.ReleasedCapitalAmount,
            FundCount = funds.Count > 0 ? funds.Count : metrics.FundCount,
            Funds = funds,
            CapitalDeployed = capitalDeployed
        };
    }

    private async Task<PagedResult<InvestorInvestmentDto>> GetInvestorFundsInternalAsync(
        long investorKey,
        TimeGranularity view,
        FundPeriodFilter? period,
        int page,
        int pageSize)
    {
        var factTable = PortfolioFactTable(view);
        var countSql = BuildInvestorTransactionCountSql(factTable, view, period);

        var pageSql = new StringBuilder();
        pageSql.Append(" select ");
        pageSql.Append(" f.fund_key, ");
        pageSql.Append(" isnull(f.fund_code, '') as fund_code, ");
        pageSql.Append(" max(isnull(f.fund_name, '')) as fund_name, ");
        pageSql.Append(" max(isnull(f.fund_type_name, '')) as fund_type, ");
        pageSql.Append(" max(isnull(f.fund_strategy_name, isnull(f.fund_type_name, ''))) as fund_category, ");
        pageSql.Append(" case ");
        pageSql.Append(" when max(case when f.dissolution_date is not null then 1 else 0 end) = 1 then 'Dissolved' ");
        pageSql.Append(" when max(case when isnull(f.is_current, 1) = 1 then 1 else 0 end) = 1 then 'Active' ");
        pageSql.Append(" else 'Inactive' ");
        pageSql.Append(" end as fund_status, ");
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

                return new InvestorInvestmentDto
                {
                    FundKey = reader.GetInt32OrDefault("fund_key"),
                    FundCode = reader.GetStringOrEmpty("fund_code"),
                    FundName = reader.GetStringOrEmpty("fund_name"),
                    FundType = reader.GetStringOrEmpty("fund_type"),
                    FundCategory = reader.GetStringOrEmpty("fund_category"),
                    Status = reader.GetStringOrEmpty("fund_status"),
                    CommitmentAmount = commitment,
                    NetInvestedCapitalAmount = netInvested,
                    NetDistributedAmount = reader.GetDecimalOrDefault("net_distributed_amount"),
                    ReservedAmount = reader.GetDecimalOrDefault("reserved_amount"),
                    UnfundedAmount = reader.GetDecimalOrDefault("unfunded_amount"),
                    ReleasedCapitalAmount = reader.GetNullableDecimal("released_capital_amount"),
                    InvestedPercent = PortalPortfolioMetrics.ComputeInvestedPercent(commitment, netInvested),
                    InvestedAmount = commitment,
                    InvestedAmountFmv = netInvested
                };
            });
    }
}
