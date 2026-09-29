using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Configuration;

/// <summary>Builds Azure credentials from the Fabric warehouse SQL connection string (service principal).</summary>
internal static class FabricServicePrincipalCredentials
{
    private static readonly string[] OneLakeScopes = ["https://storage.azure.com/.default"];

    public static TokenCredential Create(
        IConfiguration configuration,
        ILogger logger)
    {
        var connectionString = configuration.GetConnectionString("FabricConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configuration key 'FabricConnectionString' is missing.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(
                builder.Authentication.ToString(),
                "ActiveDirectoryServicePrincipal",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "FabricConnectionString must use Authentication=ActiveDirectoryServicePrincipal for OneLake access.");
        }

        var clientId = builder.UserID;
        var clientSecret = builder.Password;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "FabricConnectionString must include User ID (app id) and Password (client secret) for OneLake access.");
        }

        var tenantId = configuration.GetSection("AzureAd")["TenantId"];
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("AzureAd:TenantId is required for OneLake service principal auth.");
        }

        logger.LogDebug("Using service principal {ClientId} for Fabric OneLake storage.", clientId);
        var inner = new ClientSecretCredential(tenantId, clientId, clientSecret);
        return new OneLakeTokenCredential(inner);
    }

    /// <summary>Requests a storage-scoped token suitable for OneLake ADLS APIs.</summary>
    private sealed class OneLakeTokenCredential(TokenCredential inner) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var context = new TokenRequestContext(OneLakeScopes);
            return inner.GetToken(context, cancellationToken);
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            var context = new TokenRequestContext(OneLakeScopes);
            return inner.GetTokenAsync(context, cancellationToken);
        }
    }
}
