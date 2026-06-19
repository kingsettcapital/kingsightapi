using Azure.Storage.Files.DataLake;
using kingsightapi.Services;

namespace kingsightapi.Configuration;

internal static class CmhcFileStorageExtensions
{
    public static IServiceCollection AddCmhcFileStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CmhcUploadOptions>(configuration.GetSection(CmhcUploadOptions.SectionName));

        var options = configuration.GetSection(CmhcUploadOptions.SectionName).Get<CmhcUploadOptions>()
            ?? new CmhcUploadOptions();

        if (options.UsesFabricStorage)
        {
            services.AddSingleton<DataLakeServiceClient>(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CmhcUploadOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<FabricCmhcFileStorage>>();
                var credential = FabricServicePrincipalCredentials.Create(configuration, logger);
                return new DataLakeServiceClient(new Uri(opts.FabricServiceUri), credential);
            });
            services.AddScoped<ICmhcFileStorage, FabricCmhcFileStorage>();
        }
        else
        {
            services.AddScoped<ICmhcFileStorage, LocalCmhcFileStorage>();
        }

        services.AddScoped<ICmhcUploadService, CmhcUploadService>();
        return services;
    }
}
