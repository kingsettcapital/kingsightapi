using kingsightapi.Configuration;
using kingsightapi.Entities;
using kingsightapi.Services;
using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace kingsightapi
{
   
    public class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var log4netConfigPath = builder.Configuration.GetSection("log4netConfigFile")?.Value;
            if (string.IsNullOrWhiteSpace(log4netConfigPath))
            {
                throw new InvalidOperationException("log4netConfigFile is not configured in appsettings.");
            }
            XmlConfigurator.Configure(new FileInfo(log4netConfigPath));

            log.Info("Kingsight API starting up...");

            // log4net — logs folder is created under bin/.../logs (or publish folder/logs on server).
            //var logDirectory = Log4NetBootstrap.Configure(builder);

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

            builder.WebHost.ConfigureKestrel(options =>
            {
                // QR slide PDFs can be ~40 MB; default Kestrel limit is ~28.6 MB.
                options.Limits.MaxRequestBodySize = 62_914_560;
            });

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.Converters.Add(
                        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                });

            builder.Services.AddEntraAuthentication(configuration);
            builder.Services.Configure<FabricWarehouseOptions>(
                configuration.GetSection(FabricWarehouseOptions.SectionName));
            builder.Services.AddSingleton<FabricWarehouseTables>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentUserResolver, CurrentUserResolver>();

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
            builder.Services.AddSingleton<IDataExplorerService, DataExplorerService>();
            builder.Services.AddSingleton<IDefaultDateCaptureService, DefaultDateCaptureService>();
            builder.Services.AddSingleton<IDefaultSubjectiveAnalyticsService, DefaultSubjectiveAnalyticsService>();
            builder.Services.AddSingleton<ITaxArrearsService, TaxArrearsService>();
            builder.Services.AddSingleton<ILtvValidationService, LtvValidationService>();
            builder.Services.AddSingleton<INonKsLoanAliasBridge, NonKsLoanAliasBridge>();
            builder.Services.AddSingleton<INonKsInvestorAliasBridge, NonKsInvestorAliasBridge>();
            builder.Services.AddSingleton<INonKsServicedLoansService, NonKsServicedLoansService>();
            builder.Services.AddSingleton<INotificationService, NotificationService>();
            builder.Services.AddSingleton<IManagementSummaryService, ManagementSummaryService>();
            builder.Services.AddSingleton<IRoleService, RoleService>();
            builder.Services.AddSingleton<IUserService, UserService>();

            builder.Services.AddCmhcFileStorage(configuration);
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 62_914_560;
            });

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
            var warehouseOptions = configuration
                .GetSection(FabricWarehouseOptions.SectionName)
                .Get<FabricWarehouseOptions>() ?? new FabricWarehouseOptions();
            WarehouseTables.Configure(warehouseOptions);
            var cmhcUploadOptions = configuration
                .GetSection(CmhcUploadOptions.SectionName)
                .Get<CmhcUploadOptions>() ?? new CmhcUploadOptions();

            var app = builder.Build();

            var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            startupLogger.LogInformation("Kingsight API started.");
            startupLogger.LogInformation(
            "Environment={Environment}; FabricWarehouse SubjectiveInputDatabase={SubjectiveDb}, Database={Database}, Silver={Silver}, Bronze={Bronze}; CmhcUpload Workspace={WorkspaceId} Lakehouse={LakehouseId} Path=Files/{UploadPath}",
                app.Environment.EnvironmentName,
                warehouseOptions.SubjectiveInputDatabase,
                warehouseOptions.Database,
                warehouseOptions.SilverLakehouseDatabase,
                warehouseOptions.BronzeLakehouseDatabase,
                cmhcUploadOptions.FabricWorkspaceId,
                cmhcUploadOptions.FabricLakehouseId,
                cmhcUploadOptions.UploadParentDirectory);

            if (string.IsNullOrWhiteSpace(fabricConnectionString))
            {
                startupLogger.LogError(
                    "FabricConnectionString is missing. Portal endpoints will fail at runtime.");
            }

            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    scope.ServiceProvider.GetRequiredService<ICmhcFileStorage>().EnsureStorageReady();
                }
                catch (Exception ex)
                {
                    startupLogger.LogWarning(
                        ex,
                        "CMHC storage preflight failed; the API will start but upload endpoints may fail until OneLake is reachable.");
                }
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
    }
}
