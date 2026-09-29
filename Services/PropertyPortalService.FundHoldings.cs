using System.Text;
using kingsightapi.Entities;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Services;

public sealed partial class PropertyPortalService
{
    public async Task<IReadOnlyList<PropertyFundHoldingDto>> GetPropertyFundHoldingsAsync(long propertyKey)
    {
        try
        {
            return await GetPropertyFundHoldingsInternalAsync(propertyKey);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Get fund holdings for property {PropertyKey} cancelled", propertyKey);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fund holdings for property {PropertyKey}", propertyKey);
            throw;
        }
    }

    private async Task<IReadOnlyList<PropertyFundHoldingDto>> GetPropertyFundHoldingsInternalAsync(long propertyKey)
    {
        var sql = new StringBuilder();
        sql.Append(" select ");
        sql.Append(" isnull(p.property_code, '') as property_code, ");
        sql.Append(" f.fund_key, ");
        sql.Append(" isnull(f.fund_code, '') as fund_code, ");
        sql.Append(" isnull(f.fund_name, '') as fund_name, ");
        sql.Append(" isnull(f.fund_strategy_name, '') as fund_strategy_name, ");
        sql.Append(" isnull(f.fund_type_name, '') as fund_type_name, ");
        sql.Append(" f.fund_start_date ");
        sql.Append($" from {WarehouseTables.DimProperty} p ");
        sql.Append($" inner join {WarehouseTables.DimFund} f on isnull(p.fund, '') = isnull(f.fund_code, '') ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentFundFilter(sql, "f");
        sql.Append(" where p.property_key = @propertyKey ");
        sql.Append(" and ");
        WarehouseSql.AppendCurrentPropertyFilter(sql, "p");
        sql.Append(" order by f.fund_name ");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql.ToString(), connection)
        {
            CommandType = System.Data.CommandType.Text
        };
        command.Parameters.AddWithValue("@propertyKey", propertyKey);

        var items = new List<PropertyFundHoldingDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new PropertyFundHoldingDto
            {
                PropertyCode = reader.GetStringOrEmpty("property_code"),
                FundKey = reader.GetInt32OrDefault("fund_key"),
                FundCode = reader.GetStringOrEmpty("fund_code"),
                FundName = reader.GetStringOrEmpty("fund_name"),
                FundStrategyName = reader.GetStringOrEmpty("fund_strategy_name"),
                FundTypeName = reader.GetStringOrEmpty("fund_type_name"),
                FundStartDate = reader.GetNullableDateTime("fund_start_date")
            });
        }

        return items;
    }
}
