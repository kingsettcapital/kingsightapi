using System.Text.RegularExpressions;
using kingsightapi.Entities;

namespace kingsightapi.Services;

/// <summary>
/// Parses SharePoint library browser URLs into site + library/folder server-relative paths.
/// Example: https://tenant.sharepoint.com/Reporting/Growth%20LPs/Forms/AllItems.aspx
/// → site https://tenant.sharepoint.com/Reporting, library /Reporting/Growth LPs
/// </summary>
public static class SharePointLibraryUrlParser
{
    private static readonly Regex FormsSegmentRegex = new(
        @"/Forms(/|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static SharePointLibraryTarget? TryParse(string? rawUrl, string? fallbackSiteUrl = null)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        var queryId = GetQueryValue(uri.Query, "id");
        if (!string.IsNullOrWhiteSpace(queryId))
        {
            var decoded = Uri.UnescapeDataString(queryId).Replace('\\', '/').Trim();
            if (!decoded.StartsWith('/'))
            {
                decoded = "/" + decoded;
            }

            return BuildFromServerRelativePath(uri, decoded, fallbackSiteUrl);
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath).Replace('\\', '/');
        var formsMatch = FormsSegmentRegex.Match(path);
        if (formsMatch.Success)
        {
            path = path[..formsMatch.Index];
        }

        path = path.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return null;
        }

        return BuildFromServerRelativePath(uri, path, fallbackSiteUrl);
    }

    private static string? GetQueryValue(string query, string key)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        var trimmed = query.StartsWith('?') ? query[1..] : query;
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var name = eq >= 0 ? part[..eq] : part;
            if (!string.Equals(Uri.UnescapeDataString(name), key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return eq >= 0 ? part[(eq + 1)..] : string.Empty;
        }

        return null;
    }

    private static SharePointLibraryTarget? BuildFromServerRelativePath(
        Uri sourceUri,
        string serverRelativePath,
        string? fallbackSiteUrl)
    {
        var segments = serverRelativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        string siteUrl;
        string libraryServerRelative;
        string? folderServerRelative = null;
        string libraryTitle;

        if (segments.Length == 1)
        {
            siteUrl = !string.IsNullOrWhiteSpace(fallbackSiteUrl)
                ? fallbackSiteUrl.TrimEnd('/')
                : sourceUri.GetLeftPart(UriPartial.Authority);
            libraryTitle = segments[0];
            libraryServerRelative = "/" + libraryTitle;
        }
        else
        {
            var siteWeb = segments[0];
            libraryTitle = segments[1];
            siteUrl = $"{sourceUri.GetLeftPart(UriPartial.Authority)}/{siteWeb}";
            libraryServerRelative = $"/{siteWeb}/{libraryTitle}";

            if (segments.Length > 2)
            {
                folderServerRelative = "/" + string.Join('/', segments);
            }
        }

        return new SharePointLibraryTarget
        {
            SiteUrl = siteUrl,
            LibraryServerRelativeUrl = libraryServerRelative,
            FolderServerRelativeUrl = folderServerRelative,
            LibraryTitle = libraryTitle,
        };
    }

    /// <summary>
    /// Prefer persisted parsed fields when present; otherwise parse <see cref="FundSharePointLibraryRow.SharePointUrl"/>.
    /// </summary>
    public static SharePointLibraryTarget? ResolveTarget(
        FundSharePointLibraryRow row,
        string? fallbackSiteUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(row.SiteUrl)
            && !string.IsNullOrWhiteSpace(row.LibraryServerRelativeUrl))
        {
            return new SharePointLibraryTarget
            {
                SiteUrl = row.SiteUrl.TrimEnd('/'),
                LibraryServerRelativeUrl = row.LibraryServerRelativeUrl.TrimEnd('/'),
                FolderServerRelativeUrl = string.IsNullOrWhiteSpace(row.FolderServerRelativeUrl)
                    ? null
                    : row.FolderServerRelativeUrl.TrimEnd('/'),
                LibraryTitle = row.LibraryTitle,
            };
        }

        return TryParse(row.SharePointUrl, fallbackSiteUrl);
    }
}
