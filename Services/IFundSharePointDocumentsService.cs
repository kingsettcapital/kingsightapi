using kingsightapi.Entities;

namespace kingsightapi.Services;

public interface IFundSharePointDocumentsService
{
    /// <summary>
    /// Lists Interim/Annual Reports for a fund using the SharePoint library URL
    /// stored in <c>investor_servicing.fund_sharepoint_library</c>. Successful lists
    /// refresh <c>investor_servicing.fund_document</c>; failures fall back to cache.
    /// </summary>
    Task<FundDocumentsResultDto> GetFundDocumentsAsync(int fundKey, CancellationToken cancellationToken = default);

    /// <summary>Upserts the browser SharePoint library URL for a fund.</summary>
    Task UpsertFundLibraryUrlAsync(
        int fundKey,
        string sharePointUrl,
        string? auditUser = null,
        CancellationToken cancellationToken = default);
}
