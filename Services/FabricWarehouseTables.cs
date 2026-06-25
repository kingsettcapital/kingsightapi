using kingsightapi.Configuration;
using Microsoft.Extensions.Options;

namespace kingsightapi.Services;

/// <summary>Builds fully qualified Fabric table names for mortgage and input schemas.</summary>
public sealed class FabricWarehouseTables
{
    private readonly FabricWarehouseOptions _options;

    public FabricWarehouseTables(IOptions<FabricWarehouseOptions> options)
    {
        _options = options.Value;
    }

    public string Database => _options.Database;

    public string Mort(string table) => $"{Database}.mort.{table}";

    public string Input(string table) => $"{Database}.input.{table}";
}
