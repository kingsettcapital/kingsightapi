using System.Text.Json;
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
            if (builder.Environment.IsDevelopment())
            {
                // HTTPS for SPA default; HTTP avoids local dev-cert issues in the browser.
                builder.WebHost.UseUrls("https://localhost:7140", "http://localhost:5181");
            }
            else
            {
                builder.WebHost.UseUrls("https://localhost:7140");
            }

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

            var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:4200"];

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularDev", policy =>
                    policy.WithOrigins(corsOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod());
            });

            var app = builder.Build();

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
            
            app.UseRouting();
            app.UseCors("AllowAngularDev");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.Run();
        }
    }
}
