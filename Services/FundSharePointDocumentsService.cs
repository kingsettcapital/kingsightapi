using System.Globalization;
using System.Text.RegularExpressions;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using Microsoft.Extensions.Options;
using Microsoft.SharePoint.Client;
using File = Microsoft.SharePoint.Client.File;

namespace kingsightapi.Services;

public sealed class FundSharePointDocumentsService : IFundSharePointDocumentsService
{
    private const string DefaultCategory = "Interim/Annual Reports";

    private static readonly Regex FileNameMetaRegex = new(
        @"-(?<quarter>Q[1-4])-(?<year>20\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly SharePointContextFactory _contextFactory;
    private readonly IFundPortalService _fundPortal;
    private readonly IFundSharePointDocumentsStore _store;
    private readonly SharePointOptions _options;
    private readonly ILogger<FundSharePointDocumentsService> _logger;

    public FundSharePointDocumentsService(
        SharePointContextFactory contextFactory,
        IFundPortalService fundPortal,
        IFundSharePointDocumentsStore store,
        IOptions<SharePointOptions> options,
        ILogger<FundSharePointDocumentsService> logger)
    {
        _contextFactory = contextFactory;
        _fundPortal = fundPortal;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FundDocumentsResultDto> GetFundDocumentsAsync(
        int fundKey,
        CancellationToken cancellationToken = default)
    {
        var profile = await _fundPortal.GetFundByKeyAsync(fundKey).ConfigureAwait(false);
        if (profile is null)
        {
            throw new KeyNotFoundException($"Fund {fundKey} was not found.");
        }

        var fundCode = (profile.FundCode ?? string.Empty).Trim();
        var category = string.IsNullOrWhiteSpace(_options.InterimAnnualReportsFolder)
            ? DefaultCategory
            : _options.InterimAnnualReportsFolder.Trim().Trim('/');

        var libraryRow = await _store.GetLibraryAsync(fundKey, category, cancellationToken)
            .ConfigureAwait(false);

        if (libraryRow is null)
        {
            _logger.LogWarning(
                "No SharePoint library URL mapped for fund {FundKey} ({FundCode}). Insert into investor_servicing.fund_sharepoint_library.",
                fundKey,
                fundCode);
            return EmptyResult(fundKey, fundCode, category, folderPath: string.Empty);
        }

        var target = SharePointLibraryUrlParser.ResolveTarget(libraryRow, _options.SiteUrl);
        if (target is null)
        {
            _logger.LogWarning(
                "Could not parse SharePoint URL for fund {FundKey}: {Url}",
                fundKey,
                libraryRow.SharePointUrl);
            return await FallbackToCacheAsync(fundKey, fundCode, category, libraryRow.SharePointUrl, cancellationToken)
                .ConfigureAwait(false);
        }

        // Persist parsed paths for faster subsequent calls / ops visibility.
        if (string.IsNullOrWhiteSpace(libraryRow.SiteUrl)
            || string.IsNullOrWhiteSpace(libraryRow.LibraryServerRelativeUrl))
        {
            try
            {
                await _store.UpsertLibraryAsync(
                        new FundSharePointLibraryRow
                        {
                            FundKey = fundKey,
                            FundCode = fundCode,
                            Category = category,
                            SharePointUrl = libraryRow.SharePointUrl,
                            SiteUrl = target.SiteUrl,
                            LibraryServerRelativeUrl = target.LibraryServerRelativeUrl,
                            FolderServerRelativeUrl = target.FolderServerRelativeUrl,
                            LibraryTitle = target.LibraryTitle,
                            IsActive = true,
                        },
                        auditUser: "documents-service",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not backfill parsed SharePoint paths for fund {FundKey}", fundKey);
            }
        }

        if (!_options.Enabled || !_contextFactory.IsConfigured)
        {
            _logger.LogWarning(
                "SharePoint disabled or incomplete cert config; using DB cache for fund {FundKey}",
                fundKey);
            return await FallbackToCacheAsync(
                    fundKey,
                    fundCode,
                    category,
                    target.ListFolderServerRelativeUrl,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            using var context = await _contextFactory
                .CreateContextAsync(target.SiteUrl, cancellationToken)
                .ConfigureAwait(false);

            var folderPath = target.ListFolderServerRelativeUrl;
            var folder = context.Web.GetFolderByServerRelativeUrl(folderPath);
            context.Load(
                folder.Files,
                files => files.Include(
                    f => f.Name,
                    f => f.Length,
                    f => f.TimeLastModified,
                    f => f.ServerRelativeUrl,
                    f => f.UniqueId,
                    f => f.ListItemAllFields));
            await context.ExecuteQueryAsync().ConfigureAwait(false);

            var siteUri = new Uri(target.SiteUrl.TrimEnd('/') + "/");
            var items = folder.Files
                .Select(file => MapFile(file, siteUri))
                .OrderByDescending(item => item.Year ?? 0)
                .ThenByDescending(item => item.Quarter ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(item => item.ModifiedOn)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await _store.ReplaceCachedDocumentsAsync(fundKey, category, items, cancellationToken)
                .ConfigureAwait(false);

            return new FundDocumentsResultDto
            {
                FundKey = fundKey,
                FundCode = fundCode,
                Category = category,
                FolderPath = folderPath,
                Items = items,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not KeyNotFoundException)
        {
            _logger.LogError(
                ex,
                "Failed to list SharePoint documents for fund {FundKey} ({FundCode}) at '{Folder}'. Falling back to DB cache.",
                fundKey,
                fundCode,
                target.ListFolderServerRelativeUrl);

            return await FallbackToCacheAsync(
                    fundKey,
                    fundCode,
                    category,
                    target.ListFolderServerRelativeUrl,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task UpsertFundLibraryUrlAsync(
        int fundKey,
        string sharePointUrl,
        string? auditUser = null,
        CancellationToken cancellationToken = default)
    {
        var profile = await _fundPortal.GetFundByKeyAsync(fundKey).ConfigureAwait(false);
        if (profile is null)
        {
            throw new KeyNotFoundException($"Fund {fundKey} was not found.");
        }

        var fundCode = (profile.FundCode ?? string.Empty).Trim();
        var category = string.IsNullOrWhiteSpace(_options.InterimAnnualReportsFolder)
            ? DefaultCategory
            : _options.InterimAnnualReportsFolder.Trim().Trim('/');

        var target = SharePointLibraryUrlParser.TryParse(sharePointUrl, _options.SiteUrl)
            ?? throw new ArgumentException("SharePoint URL could not be parsed.", nameof(sharePointUrl));

        await _store.UpsertLibraryAsync(
                new FundSharePointLibraryRow
                {
                    FundKey = fundKey,
                    FundCode = fundCode,
                    Category = category,
                    SharePointUrl = sharePointUrl.Trim(),
                    SiteUrl = target.SiteUrl,
                    LibraryServerRelativeUrl = target.LibraryServerRelativeUrl,
                    FolderServerRelativeUrl = target.FolderServerRelativeUrl,
                    LibraryTitle = target.LibraryTitle,
                    IsActive = true,
                },
                auditUser: string.IsNullOrWhiteSpace(auditUser) ? "api" : auditUser.Trim(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<FundDocumentsResultDto> FallbackToCacheAsync(
        int fundKey,
        string fundCode,
        string category,
        string folderPath,
        CancellationToken cancellationToken)
    {
        var cached = await _store.GetCachedDocumentsAsync(fundKey, category, cancellationToken)
            .ConfigureAwait(false);
        return new FundDocumentsResultDto
        {
            FundKey = fundKey,
            FundCode = fundCode,
            Category = category,
            FolderPath = folderPath,
            Items = cached,
        };
    }

    private static FundDocumentItemDto MapFile(File file, Uri siteUri)
    {
        var fields = file.ListItemAllFields;
        var year = ReadIntField(fields, "Year") ?? ParseYearFromName(file.Name);
        var quarter = ReadStringField(fields, "Quarter") ?? ParseQuarterFromName(file.Name);
        var modifiedBy = ReadUserTitle(fields, "Editor") ?? ReadUserTitle(fields, "Author");
        var webUrl = BuildWebUrl(siteUri, file.ServerRelativeUrl);

        return new FundDocumentItemDto
        {
            Id = file.UniqueId.ToString("N"),
            Name = file.Name,
            Year = year,
            Quarter = quarter,
            ModifiedOn = file.TimeLastModified == default ? null : file.TimeLastModified.ToUniversalTime(),
            ModifiedBy = modifiedBy,
            SizeBytes = file.Length > 0 ? file.Length : null,
            WebUrl = webUrl,
            ServerRelativeUrl = file.ServerRelativeUrl,
        };
    }

    private static string BuildWebUrl(Uri siteUri, string? serverRelativeUrl)
    {
        if (string.IsNullOrWhiteSpace(serverRelativeUrl))
        {
            return siteUri.ToString();
        }

        if (Uri.TryCreate(siteUri, serverRelativeUrl, out var absolute))
        {
            return absolute.ToString();
        }

        return $"{siteUri.GetLeftPart(UriPartial.Authority)}{serverRelativeUrl}";
    }

    private static int? ReadIntField(ListItem fields, string fieldName)
    {
        try
        {
            if (!fields.FieldValues.TryGetValue(fieldName, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    => parsed,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadStringField(ListItem fields, string fieldName)
    {
        try
        {
            if (!fields.FieldValues.TryGetValue(fieldName, out var value) || value is null)
            {
                return null;
            }

            var text = value.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadUserTitle(ListItem fields, string fieldName)
    {
        try
        {
            if (!fields.FieldValues.TryGetValue(fieldName, out var value) || value is null)
            {
                return null;
            }

            if (value is FieldUserValue user)
            {
                return string.IsNullOrWhiteSpace(user.LookupValue) ? null : user.LookupValue.Trim();
            }

            var text = value.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private static int? ParseYearFromName(string fileName)
    {
        var match = FileNameMetaRegex.Match(fileName ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups["year"].Value, out var year) ? year : null;
    }

    private static string? ParseQuarterFromName(string fileName)
    {
        var match = FileNameMetaRegex.Match(fileName ?? string.Empty);
        return match.Success ? match.Groups["quarter"].Value.ToUpperInvariant() : null;
    }

    private static FundDocumentsResultDto EmptyResult(
        int fundKey,
        string fundCode,
        string category,
        string folderPath) =>
        new()
        {
            FundKey = fundKey,
            FundCode = fundCode,
            Category = category,
            FolderPath = folderPath,
            Items = [],
        };
}
