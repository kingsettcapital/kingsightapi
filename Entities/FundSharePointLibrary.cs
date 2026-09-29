namespace kingsightapi.Entities;

/// <summary>DB row: fund → SharePoint library/folder URL for Interim/Annual Reports.</summary>
public sealed class FundSharePointLibraryRow
{
    public int FundKey { get; init; }

    public string? FundCode { get; init; }

    public string Category { get; init; } = "Interim/Annual Reports";

    /// <summary>Raw browser URL pasted by ops (Forms/AllItems.aspx or folder deep link).</summary>
    public string SharePointUrl { get; init; } = string.Empty;

    public string? SiteUrl { get; init; }

    public string? LibraryServerRelativeUrl { get; init; }

    public string? FolderServerRelativeUrl { get; init; }

    public string? LibraryTitle { get; init; }

    public bool IsActive { get; init; } = true;
}

/// <summary>Parsed target used by CSOM listing.</summary>
public sealed class SharePointLibraryTarget
{
    public string SiteUrl { get; init; } = string.Empty;

    public string LibraryServerRelativeUrl { get; init; } = string.Empty;

    public string? FolderServerRelativeUrl { get; init; }

    public string? LibraryTitle { get; init; }

    /// <summary>Folder to list: folder path if present, otherwise library root.</summary>
    public string ListFolderServerRelativeUrl =>
        string.IsNullOrWhiteSpace(FolderServerRelativeUrl)
            ? LibraryServerRelativeUrl
            : FolderServerRelativeUrl;
}
