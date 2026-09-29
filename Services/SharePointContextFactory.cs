using kingsightapi.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using System.Security.Cryptography.X509Certificates;

namespace kingsightapi.Services;

/// <summary>
/// Certificate app-only SharePoint ClientContext factory
/// (adapted from KS.TB.AUTOMATION SharePoint helper).
/// </summary>
public sealed class SharePointContextFactory
{
    private readonly SharePointOptions _options;
    private readonly ILogger<SharePointContextFactory> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresOn = DateTimeOffset.MinValue;

    public SharePointContextFactory(
        IOptions<SharePointOptions> options,
        ILogger<SharePointContextFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.ClientId)
        && !string.IsNullOrWhiteSpace(_options.SiteUrl)
        && !string.IsNullOrWhiteSpace(_options.Authority)
        && _options.Scopes is { Length: > 0 }
        && !string.IsNullOrWhiteSpace(_options.CertificatePath);

    public async Task<ClientContext> CreateContextAsync(CancellationToken cancellationToken = default) =>
        await CreateContextAsync(siteUrl: null, cancellationToken).ConfigureAwait(false);

    /// <param name="siteUrl">
    /// Optional per-request site (from DB-mapped fund library URL). Falls back to SharePoint:SiteUrl.
    /// </param>
    public async Task<ClientContext> CreateContextAsync(
        string? siteUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("SharePoint is not configured or is disabled.");
        }

        var resolvedSite = string.IsNullOrWhiteSpace(siteUrl)
            ? _options.SiteUrl
            : siteUrl.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(resolvedSite))
        {
            throw new InvalidOperationException("SharePoint site URL is missing.");
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var context = new ClientContext(resolvedSite);
        context.ExecutingWebRequest += (_, e) =>
        {
            e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + accessToken;
        };
        return context;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrEmpty(_cachedToken)
                && _tokenExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return _cachedToken;
            }

            using var certificate = LoadCertificate();
            var app = ConfidentialClientApplicationBuilder
                .Create(_options.ClientId)
                .WithCertificate(certificate)
                .WithAuthority(_options.Authority)
                .Build();

            var result = await app
                .AcquireTokenForClient(_options.Scopes)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);

            _cachedToken = result.AccessToken;
            _tokenExpiresOn = result.ExpiresOn;
            _logger.LogDebug("Acquired SharePoint app-only access token; expires {ExpiresOn}", result.ExpiresOn);
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private X509Certificate2 LoadCertificate()
    {
        if (!System.IO.File.Exists(_options.CertificatePath))
        {
            throw new FileNotFoundException(
                $"SharePoint certificate not found at '{_options.CertificatePath}'.",
                _options.CertificatePath);
        }

        return new X509Certificate2(
            _options.CertificatePath,
            _options.CertificatePassword,
            X509KeyStorageFlags.MachineKeySet
                | X509KeyStorageFlags.PersistKeySet
                | X509KeyStorageFlags.Exportable);
    }
}
