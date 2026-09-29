using Microsoft.Data.SqlClient;
using System.Text.Json;

var appsettings = await File.ReadAllTextAsync(@"C:\Code\kingsightapi\appsettings.json");
using var doc = JsonDocument.Parse(appsettings);
var cs = doc.RootElement.GetProperty("ConnectionStrings").GetProperty("FabricConnectionString").GetString()
    ?? throw new InvalidOperationException("missing connection string");

var createSql = await File.ReadAllTextAsync(@"C:\Code\kingsightapi\Scripts\Create_investor_servicing_fund_sharepoint_documents.sql");
var seedSql = await File.ReadAllTextAsync(@"C:\Code\kingsightapi\Scripts\Seed_investor_servicing_fund_sharepoint_library.sql");

await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
Console.WriteLine("Connected to Fabric.");

await ExecBatchesAsync(conn, createSql, "CREATE");
await ExecBatchesAsync(conn, seedSql, "SEED");

await using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = """
        select fund_key, fund_code, notes, sharepoint_url
        from investor_servicing.fund_sharepoint_library
        where category = 'Interim/Annual Reports'
          and upper(isnull(is_active, 'Y')) in ('Y', '1', 'T')
        order by fund_code;
        """;
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        count++;
        Console.WriteLine($"{reader.GetInt32(0)}\t{reader.GetString(1)}\t{reader.GetString(2)}\t{reader.GetString(3)}");
    }
    Console.WriteLine($"Mapped rows: {count}");
}

static async Task ExecBatchesAsync(SqlConnection conn, string script, string label)
{
    var batches = script.Split(["\r\nGO\r\n", "\nGO\n", "\r\nGO", "\nGO"], StringSplitOptions.RemoveEmptyEntries);
    foreach (var batch in batches)
    {
        var sql = batch.Trim();
        if (string.IsNullOrWhiteSpace(sql) || sql.StartsWith("--") && !sql.Contains('\n'))
        {
            continue;
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 120;
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            var sets = 0;
            do
            {
                var rows = 0;
                while (await reader.ReadAsync())
                {
                    rows++;
                    if (reader.FieldCount >= 4)
                    {
                        Console.WriteLine($"[seed-result] {reader.GetValue(0)}\t{reader.GetValue(1)}\t{reader.GetValue(2)}\t{reader.GetValue(3)}");
                    }
                }
                if (rows > 0)
                {
                    sets++;
                    Console.WriteLine($"{label} result set rows: {rows}");
                }
            } while (await reader.NextResultAsync());
            if (sets == 0)
            {
                Console.WriteLine($"{label}: batch OK (no rows)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{label} ERROR: {ex.Message}");
            throw;
        }
    }
}
