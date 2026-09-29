using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using File = Microsoft.SharePoint.Client.File;

var appsettings = JsonDocument.Parse(await System.IO.File.ReadAllTextAsync(@"C:\Code\kingsightapi\appsettings.json"));
var cs = appsettings.RootElement.GetProperty("ConnectionStrings").GetProperty("FabricConnectionString").GetString()!;
var sp = appsettings.RootElement.GetProperty("SharePoint");
var clientId = sp.GetProperty("ClientId").GetString()!;
var authority = sp.GetProperty("Authority").GetString()!;
var scopes = sp.GetProperty("Scopes").EnumerateArray().Select(x => x.GetString()!).ToArray();
var certPath = sp.GetProperty("CertificatePath").GetString()!;
var certPassword = sp.GetProperty("CertificatePassword").GetString()!;

await using var sql = new SqlConnection(cs);
await sql.OpenAsync();
await using var cmd = sql.CreateCommand();
cmd.CommandText = """
    select top 1 sharepoint_url
    from investor_servicing.fund_sharepoint_library
    where fund_key = 12 and category = 'Interim/Annual Reports'
    """;
var url = (string?)await cmd.ExecuteScalarAsync();
Console.WriteLine($"DB URL fund 12: {url}");
if (string.IsNullOrWhiteSpace(url)) { return; }

var uri = new Uri(url);
var path = Uri.UnescapeDataString(uri.AbsolutePath);
var formsIdx = path.IndexOf("/Forms", StringComparison.OrdinalIgnoreCase);
if (formsIdx > 0) path = path[..formsIdx];
path = path.TrimEnd('/');
var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
var siteUrl = $"{uri.GetLeftPart(UriPartial.Authority)}/{parts[0]}";
var libraryPath = "/" + string.Join('/', parts);
Console.WriteLine($"Site={siteUrl}");
Console.WriteLine($"Library={libraryPath}");

using var cert = new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
var app = ConfidentialClientApplicationBuilder.Create(clientId).WithCertificate(cert).WithAuthority(authority).Build();
var token = await app.AcquireTokenForClient(scopes).ExecuteAsync();
Console.WriteLine($"Token ok, expires {token.ExpiresOn:u}");

using var ctx = new ClientContext(siteUrl);
ctx.ExecutingWebRequest += (_, e) => e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + token.AccessToken;
var folder = ctx.Web.GetFolderByServerRelativeUrl(libraryPath);
ctx.Load(folder, f => f.Exists, f => f.ServerRelativeUrl, f => f.Files.Include(x => x.Name, x => x.Length));
try
{
    await ctx.ExecuteQueryAsync();
    Console.WriteLine($"Exists={folder.Exists}; Files={folder.Files.Count}");
    foreach (var f in folder.Files.Take(10))
        Console.WriteLine($"  {f.Name}");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    if (ex.InnerException != null) Console.WriteLine($"INNER: {ex.InnerException.Message}");
}
