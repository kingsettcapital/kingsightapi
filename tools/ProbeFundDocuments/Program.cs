using kingsightapi.Configuration;
using kingsightapi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var config = new ConfigurationBuilder()
    .AddJsonFile(@"C:\Code\kingsightapi\appsettings.json", optional: false)
    .AddJsonFile(@"C:\Code\kingsightapi\appsettings.Development.json", optional: true)
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
services.AddSingleton<IConfiguration>(config);
services.Configure<SharePointOptions>(config.GetSection(SharePointOptions.SectionName));
services.Configure<FabricWarehouseOptions>(config.GetSection(FabricWarehouseOptions.SectionName));
services.AddSingleton<FabricWarehouseTables>();
services.AddSingleton<SharePointContextFactory>();
services.AddSingleton<IFundSharePointDocumentsStore, FundSharePointDocumentsStore>();
var sp = services.BuildServiceProvider();

var store = sp.GetRequiredService<IFundSharePointDocumentsStore>();
var factory = sp.GetRequiredService<SharePointContextFactory>();
var options = sp.GetRequiredService<IOptions<SharePointOptions>>().Value;

const int fundKey = 12;
const string category = "Interim/Annual Reports";

Console.WriteLine($"SharePoint Enabled={options.Enabled}, IsConfigured={factory.IsConfigured}, SiteUrl={options.SiteUrl}");

var library = await store.GetLibraryAsync(fundKey, category);
if (library is null)
{
    Console.WriteLine($"No library row for fund_key={fundKey}");
    return;
}

Console.WriteLine($"DB URL: {library.SharePointUrl}");
var target = SharePointLibraryUrlParser.ResolveTarget(library, options.SiteUrl);
if (target is null)
{
    Console.WriteLine("URL parse failed");
    return;
}

Console.WriteLine($"Parsed SiteUrl={target.SiteUrl}");
Console.WriteLine($"Parsed Library={target.LibraryServerRelativeUrl}");
Console.WriteLine($"List folder={target.ListFolderServerRelativeUrl}");

try
{
    using var context = await factory.CreateContextAsync(target.SiteUrl);
    var folder = context.Web.GetFolderByServerRelativeUrl(target.ListFolderServerRelativeUrl);
    context.Load(
        folder,
        f => f.Exists,
        f => f.ServerRelativeUrl,
        f => f.Files.Include(
            file => file.Name,
            file => file.Length,
            file => file.TimeLastModified,
            file => file.ServerRelativeUrl,
            file => file.UniqueId));
    await context.ExecuteQueryAsync();
    Console.WriteLine($"Folder exists={folder.Exists}, files={folder.Files.Count}");
    foreach (var file in folder.Files.Take(15))
    {
        Console.WriteLine($"  - {file.Name} ({file.Length} bytes)");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"SharePoint ERROR: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is not null)
    {
        Console.WriteLine($"  Inner: {ex.InnerException.Message}");
    }
}

var cached = await store.GetCachedDocumentsAsync(fundKey, category);
Console.WriteLine($"Cached docs in DB: {cached.Count}");
