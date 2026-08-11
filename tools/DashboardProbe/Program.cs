using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var config = new ConfigurationBuilder()
    .SetBasePath(@"c:\Code\kingsightapi")
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var warehouse = config.GetSection("FabricWarehouse").Get<FabricWarehouseOptions>() ?? new FabricWarehouseOptions();
var tables = new FabricWarehouseTables(Options.Create(warehouse));
var service = new ManagementSummaryService(config, NullLogger<ManagementSummaryService>.Instance, tables);

try
{
    var result = await service.GetDashboardAsync(new ManagementSummaryDashboardQuery
    {
        AsOfDate = new DateOnly(2025, 8, 31),
        Sponsor = "All",
        RiskLevels = ["ALL"],
        Statuses = ["In Default"]
    });

    Console.WriteLine(
        $"OK loans={result.Kpis.NumberOfLoans} bal={result.Kpis.TotalOutstandingBalance} ltv={result.Kpis.AverageLtv} aliases={result.LoanAliasRows.Count} watchlist={result.WatchlistRows.Count} osInt={result.OutstandingInterest.TotalOutstandingInterest}");
    foreach (var row in result.LoanAliasRows.Take(5))
    {
        Console.WriteLine($"  {row.LoanAliasKey}:{row.LoanAlias} risk={row.Risk} exp={row.TotalExposure} ltv={row.Ltv}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    if (ex.InnerException is not null)
    {
        Console.WriteLine($"INNER: {ex.InnerException.Message}");
    }

    Console.WriteLine(ex);
    Environment.ExitCode = 1;
}
