using System.Text.Json;
using System.Text.Json.Serialization;
using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.Features;

namespace kingsightapi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

            log.LogInformation("Starting Kingsight API...");
            // log4net — logs folder is created under bin/.../logs (or publish folder/logs on server).

            var logDirectory = Log4NetBootstrap.Configure(builder);

            //if (builder.Environment.IsDevelopment())
            //{
            //    // HTTPS for SPA default; HTTP avoids local dev-cert issues in the browser.
            //    builder.WebHost.UseUrls("https://localhost:7140", "http://localhost:5181");
            //}
            var configuration = builder.Configuration;
            var apiUrl = configuration.GetSection("Api").GetValue<string>("Url");
            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                builder.WebHost.UseUrls(apiUrl);
            }

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.Converters.Add(
                        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                });

            builder.Services.AddEntraAuthentication(configuration);
            log.LogInformation("Step 1...");
            builder.Services.AddSingleton<IDBService, DBService>();
            //builder.Services.AddSingleton<IFundService, FundService>();
            builder.Services.AddSingleton<ILoanService, LoanService>();
            builder.Services.AddSingleton<IInvestorService, InvestorService>();
            builder.Services.AddSingleton<ICapitalInvestorService, CapitalInvestorService>();
            builder.Services.AddSingleton<IFundService, FundService>();
            builder.Services.AddSingleton<IInvestorPortalService, InvestorPortalService>();
            builder.Services.AddSingleton<IInvestorAliasService, InvestorAliasService>();
            builder.Services.AddSingleton<ILoanAliasService, LoanAliasService>();
            builder.Services.AddSingleton<IFundPortalService, FundPortalService>();
            builder.Services.AddSingleton<IPropertyPortalService, PropertyPortalService>();
            builder.Services.AddSingleton<IPortalFilterService, PortalFilterService>();
            builder.Services.AddSingleton<IGlobalSearchService, GlobalSearchService>();
            builder.Services.AddSingleton<IDashboardService, DashboardService>();
            builder.Services.AddSingleton<ILoanSecurityValueService, LoanSecurityValueService>();
            builder.Services.AddSingleton<IOtherCostCaptureService, OtherCostCaptureService>();
            builder.Services.AddSingleton<ILoanFormService, LoanFormService>();

            builder.Services.Configure<CmhcUploadOptions>(configuration.GetSection(CmhcUploadOptions.SectionName));
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 52_428_800;
            });
            builder.Services.AddScoped<ICmhcFileStorage, LocalCmhcFileStorage>();
            builder.Services.AddScoped<ICmhcUploadService, CmhcUploadService>();
            log.LogInformation("Step 2...");
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Kingsight API",
                    Version = "v1"
                });

                EntraAuthExtensions.ConfigureBearerSwagger(options);
            });
            log.LogInformation("Step 3...");
            builder.Services.AddAngularCors(configuration, builder.Environment);

            var fabricConnectionString = configuration.GetConnectionString("FabricConnectionString");

            log.LogInformation("Kingsight API started. Log file directory: {LogDirectory}", logDirectory);

            if (string.IsNullOrWhiteSpace(fabricConnectionString))
            {
                log.LogError(
                    "FabricConnectionString is missing. Portal endpoints will fail at runtime.");
            }

            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<ICmhcFileStorage>().EnsureStorageReady();
            }
            log.LogInformation("Step 4...");
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            log.LogInformation("Step 5...");
            app.UseHttpsRedirection();
            app.UseAngularCors();
            log.LogInformation("Step 6...");
            app.UseAuthentication();
            app.UseAuthorization();
            log.LogInformation("Step 7...");
            app.MapControllers();
            app.Run();
        }
    }
}
