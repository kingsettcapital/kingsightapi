using kingsightapi.Entities;



namespace kingsightapi.Services;



public interface IFundSharePointDocumentsService

{

    /// <summary>

    /// Lists fund documents for a category.

    /// <paramref name="category"/> accepts <c>interim</c> / <c>advisory</c>

    /// (or the full SharePoint category labels). Default is Interim/Annual Reports.

    /// </summary>

    Task<FundDocumentsResultDto> GetFundDocumentsAsync(

        int fundKey,

        string? category = null,

        CancellationToken cancellationToken = default);



    /// <summary>Upserts the browser SharePoint library URL for a fund (Interim/Annual).</summary>

    Task UpsertFundLibraryUrlAsync(

        int fundKey,

        string sharePointUrl,

        string? auditUser = null,

        CancellationToken cancellationToken = default);

}


