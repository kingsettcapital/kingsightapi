namespace kingsightapi.Configuration;

/// <summary>
/// SharePoint Online auth settings for fund documents.
/// Per-fund library URLs live in investor_servicing.fund_sharepoint_library (not here).
/// </summary>
public sealed class SharePointOptions
{
    public const string SectionName = "SharePoint";

    /// <summary>When false, documents endpoints skip live SharePoint and use DB cache only.</summary>
    public bool Enabled { get; set; } = true;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Default site used for token/context fallback
    /// (e.g. https://kingsettcapital.sharepoint.com/Reporting).
    /// Per-fund site is parsed from the stored library URL when present.
    /// </summary>
    public string SiteUrl { get; set; } = string.Empty;

    public string Authority { get; set; } = string.Empty;

    /// <summary>OAuth scopes, typically https://{tenant}.sharepoint.com/.default</summary>
    public string[] Scopes { get; set; } = [];

    public string CertificatePath { get; set; } = string.Empty;

    public string CertificatePassword { get; set; } = string.Empty;

    /// <summary>
    /// Category label / DB key for Interim/Annual Reports mapping rows.
    /// </summary>
    public string InterimAnnualReportsFolder { get; set; } = "Interim/Annual Reports";
}
