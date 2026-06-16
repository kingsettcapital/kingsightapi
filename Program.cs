using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using log4net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace kingsightapi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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
            builder.Services.AddSingleton<IDefaultDateCaptureService, DefaultDateCaptureService>();
            builder.Services.AddSingleton<IDefaultSubjectiveAnalyticsService, DefaultSubjectiveAnalyticsService>();
            builder.Services.AddSingleton<ITaxArrearsService, TaxArrearsService>();
            builder.Services.AddSingleton<ILtvValidationService, LtvValidationService>();
            builder.Services.AddSingleton<INonKsServicedLoansService, NonKsServicedLoansService>();

            builder.Services.Configure<CmhcUploadOptions>(configuration.GetSection(CmhcUploadOptions.SectionName));
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 52_428_800;
            });
            builder.Services.AddScoped<ICmhcFileStorage, LocalCmhcFileStorage>();
            builder.Services.AddScoped<ICmhcUploadService, CmhcUploadService>();

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

            builder.Services.AddAngularCors(configuration, builder.Environment);

            var fabricConnectionString = configuration.GetConnectionString("FabricConnectionString");

            var app = builder.Build();

            var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            startupLogger.LogInformation("Kingsight API started. Log file directory: {LogDirectory}", logDirectory);

            if (string.IsNullOrWhiteSpace(fabricConnectionString))
            {
                startupLogger.LogError(
                    "FabricConnectionString is missing. Portal endpoints will fail at runtime.");
            }

            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<ICmhcFileStorage>().EnsureStorageReady();
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAngularCors();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
    //    public class Program
    //    {
    //        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
    //        public static void Main(string[] args)
    //        {
    //            var builder = WebApplication.CreateBuilder(args);

    //            // log4net — logs folder is created under bin/.../logs (or publish folder/logs on server).
    //            var logDirectory = Log4NetBootstrap.Configure(builder);

    //            //if (builder.Environment.IsDevelopment())
    //            //{
    //            //    // HTTPS for SPA default; HTTP avoids local dev-cert issues in the browser.
    //            //    builder.WebHost.UseUrls("https://localhost:7140", "http://localhost:5181");
    //            //}
    //            var configuration = builder.Configuration;
    //            var apiUrl = configuration.GetSection("Api").GetValue<string>("Url");
    //            //if (!string.IsNullOrWhiteSpace(apiUrl))
    //            //{
    //            //    builder.WebHost.UseUrls(apiUrl);
    //            //}

    //            builder.Services.AddControllers()
    //                .AddJsonOptions(options =>
    //                {
    //                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    //                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    //                    options.JsonSerializerOptions.Converters.Add(
    //                        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    //                });

    //            builder.Services.AddEntraAuthentication(configuration);

    //            builder.Services.AddSingleton<IDBService, DBService>();
    //            //builder.Services.AddSingleton<IFundService, FundService>();
    //            builder.Services.AddSingleton<ILoanService, LoanService>();
    //            builder.Services.AddSingleton<IInvestorService, InvestorService>();
    //            builder.Services.AddSingleton<ICapitalInvestorService, CapitalInvestorService>();
    //            builder.Services.AddSingleton<IFundService, FundService>();
    //            builder.Services.AddSingleton<IInvestorPortalService, InvestorPortalService>();
    //            builder.Services.AddSingleton<IInvestorAliasService, InvestorAliasService>();
    //            builder.Services.AddSingleton<ILoanAliasService, LoanAliasService>();
    //            builder.Services.AddSingleton<IFundPortalService, FundPortalService>();
    //            builder.Services.AddSingleton<IPropertyPortalService, PropertyPortalService>();
    //            builder.Services.AddSingleton<IPortalFilterService, PortalFilterService>();
    //            builder.Services.AddSingleton<IGlobalSearchService, GlobalSearchService>();
    //            builder.Services.AddSingleton<IDashboardService, DashboardService>();
    //            builder.Services.AddSingleton<ILoanSecurityValueService, LoanSecurityValueService>();
    //            builder.Services.AddSingleton<IOtherCostCaptureService, OtherCostCaptureService>();
    //            builder.Services.AddSingleton<ILoanFormService, LoanFormService>();

    //            builder.Services.Configure<CmhcUploadOptions>(configuration.GetSection(CmhcUploadOptions.SectionName));
    //            builder.Services.Configure<FormOptions>(options =>
    //            {
    //                options.MultipartBodyLengthLimit = 52_428_800;
    //            });
    //            builder.Services.AddScoped<ICmhcFileStorage, LocalCmhcFileStorage>();
    //            builder.Services.AddScoped<ICmhcUploadService, CmhcUploadService>();

    //            builder.Services.AddEndpointsApiExplorer();
    //            builder.Services.AddSwaggerGen(options =>
    //            {
    //                options.SwaggerDoc("v1", new OpenApiInfo
    //                {
    //                    Title = "Kingsight API",
    //                    Version = "v1"
    //                });

    //                EntraAuthExtensions.ConfigureBearerSwagger(options);
    //            });

    //            builder.Services.AddAngularCors(configuration, builder.Environment);

    //            var fabricConnectionString = configuration.GetConnectionString("FabricConnectionString");

    //            var app = builder.Build();

    //            //var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    //            //startupLogger.LogInformation("Kingsight API started. Log file directory: {LogDirectory}", logDirectory);

    //            if (string.IsNullOrWhiteSpace(fabricConnectionString))
    //            {
    //                //startupLogger.LogError(
    //                //    "FabricConnectionString is missing. Portal endpoints will fail at runtime.");
    //            }

    //            using (var scope = app.Services.CreateScope())
    //            {
    //                scope.ServiceProvider.GetRequiredService<ICmhcFileStorage>().EnsureStorageReady();
    //            }

    //            if (app.Environment.IsDevelopment())
    //            {
    //                app.UseDeveloperExceptionPage();
    //                app.UseSwagger();
    //                app.UseSwaggerUI();
    //            }

    //            app.UseHttpsRedirection();
    //            app.UseAngularCors();

    //            app.UseAuthentication();
    //            app.UseAuthorization();

    //            app.MapControllers();
    //            app.Run();
    //        }
    //    }
}
