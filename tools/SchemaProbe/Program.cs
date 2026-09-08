using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
var config = new ConfigurationBuilder().SetBasePath(@"c:\Code\kingsightapi").AddJsonFile("appsettings.json").Build();
await using var conn = new SqlConnection(config.GetConnectionString("FabricConnectionString"));
await conn.OpenAsync();
foreach (var table in new[] { "fact_fund_financial_itd", "fact_fund_financial_quarterly" })
{
  Console.WriteLine("=== " + table + " ===");
  await using var cmd = conn.CreateCommand();
  cmd.CommandText = $@"
SELECT TOP 1 * FROM wh_gold.investor_servicing.{table} WHERE fund_key = 12";
  await using var r = await cmd.ExecuteReaderAsync();
  if (!await r.ReadAsync()) { Console.WriteLine("(no rows for fund_key=12)"); continue; }
  for (int i = 0; i < r.FieldCount; i++)
  {
    var val = r.IsDBNull(i) ? "NULL" : r.GetValue(i)?.ToString() ?? "";
    Console.WriteLine(r.GetName(i) + " = " + val);
  }
}
