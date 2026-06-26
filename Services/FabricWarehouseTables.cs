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

    /// <summary>Mortgage subjective input tables in <c>wh_gold1.subjective_input</c>.</summary>
    public string SubjectiveInput(string table) =>
        $"{_options.SubjectiveInputDatabase}.subjective_input.{table}";

    /// <summary>Shared dimension tables in <c>wh_gold1.shared</c>.</summary>
    public string Shared(string table) =>
        $"{_options.SubjectiveInputDatabase}.shared.{table}";

    /// <summary>Mortgage dimension tables in <c>wh_gold1.mortgage</c>.</summary>
    public string Mortgage(string table) =>
        $"{_options.SubjectiveInputDatabase}.mortgage.{table}";
}
