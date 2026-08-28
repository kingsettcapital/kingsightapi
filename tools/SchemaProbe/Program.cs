using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(@"c:\Code\kingsightapi")
    .AddJsonFile("appsettings.json", optional: false)
    .Build();
var cs = config.GetConnectionString("FabricConnectionString")!;
await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
await using var cmd = conn.CreateCommand();
cmd.CommandText = @"
SELECT TOP 5 COLUMN_NAME FROM investor_servicing.INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'fact_investor_portfolio_itd' ORDER BY ORDINAL_POSITION;
SELECT '---' AS x;
SELECT TOP 3 * FROM investor_servicing.fact_investor_portfolio_itd;
";
// simpler column list
cmd.CommandText = @"
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('fact_investor_portfolio_itd','fact_investor_portfolio_ltd','vw_investor_portfolio_itd','data_explorer_template')
ORDER BY TABLE_NAME, ORDINAL_POSITION;";
await using var r = await cmd.ExecuteReaderAsync();
while (await r.ReadAsync())
    Console.WriteLine($"{r.GetString(0)}.{r.GetString(1)}.{r.GetString(2)}");
