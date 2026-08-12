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

    public string SubjectiveInputDatabase => _options.SubjectiveInputDatabase;

    public string Mort(string table) => $"{Database}.mort.{table}";

    public string Input(string table) => $"{Database}.input.{table}";

    /// <summary>Portal mortgage tables in <c>{SubjectiveInputDatabase}.mort</c> (CMHC upload history, etc.).</summary>
    public string PortalMort(string table) => $"{SubjectiveInputDatabase}.mort.{table}";

    /// <summary>Portal auth tables in <c>{SubjectiveInputDatabase}.input</c> (legacy UserMst, RoleMst).</summary>
    public string PortalInput(string table) => $"{SubjectiveInputDatabase}.input.{table}";

    /// <summary>User Management in <c>{SubjectiveInputDatabase}.subjective_input</c> (user_master, role_master).</summary>
    public string SubjectiveInput(string table) =>
        $"{_options.SubjectiveInputDatabase}.subjective_input.{table}";

    /// <summary>Shared dimension tables in <c>{SubjectiveInputDatabase}.shared</c>.</summary>
    public string Shared(string table) =>
        $"{_options.SubjectiveInputDatabase}.shared.{table}";

    /// <summary>Mortgage dimension tables in <c>{SubjectiveInputDatabase}.mortgage</c>.</summary>
    public string Mortgage(string table) =>
        $"{_options.SubjectiveInputDatabase}.mortgage.{table}";

    /// <summary>Mortgage TVF/view-qualified name, e.g. <c>wh_gold1.mortgage.fn_exposure</c>.</summary>
    public string MortgageObject(string name) =>
        $"{_options.SubjectiveInputDatabase}.mortgage.{name}";

    /// <summary>Yardi tables in <c>{SilverLakehouseDatabase}.yardi</c>.</summary>
    public string Yardi(string table) =>
        $"{_options.SilverLakehouseDatabase}.yardi.{table}";

    /// <summary>Bronze lakehouse database name from config.</summary>
    public string BronzeLakehouseDatabase => _options.BronzeLakehouseDatabase;

    /// <summary>
    /// CMHC / external files tables in
    /// <c>{BronzeLakehouseDatabase}.{BronzeExternalFilesSchema}</c>
    /// (Dev: <c>external_files.cmhc_default</c>; UAT: <c>dbo.cmhc_default</c>).
    /// </summary>
    public string ExternalFiles(string table) =>
        $"{_options.BronzeLakehouseDatabase}.{_options.BronzeExternalFilesSchema}.{table}";
}
