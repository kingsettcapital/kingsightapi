using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace kingsightapi.Configuration;

/// <summary>
/// Validates Microsoft Entra access tokens sent by the Angular SPA (MSAL).
/// Login happens on the frontend only; this API does not perform sign-in or redirects.
/// </summary>
public static class EntraAuthExtensions
{
    public const string AzureAdSectionName = "AzureAd";

    /// <summary>
    /// Registers JWT bearer token validation. Expects Authorization: Bearer &lt;access_token&gt; from MSAL.
    /// </summary>
    public static IServiceCollection AddEntraAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration.GetSection(AzureAdSectionName));

        services.AddAuthorization(options =>
        {
            // Require a valid token on all endpoints unless [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    /// <summary>
    /// Swagger Bearer field — paste a token acquired from the Angular app (no OAuth login on the API).
    /// </summary>
    public static void ConfigureBearerSwagger(SwaggerGenOptions options)
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT from Angular MSAL. Example: Bearer {token}",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
}
