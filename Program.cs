using System.Text.Json;
using kingsightapi.Configuration;
using kingsightapi.Services;
using kingsightapi.Entities;
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

            // Configuration
            var configuration = builder.Configuration;


            // DI registrations
           
            // Add services to the container.
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    // Angular sends camelCase; accept it reliably even if defaults change.
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                });
            //builder.Services.AddScoped<DBService>();
            builder.Services.AddSingleton<IDBService, DBService>();
            //builder.Services.AddSingleton<IFundService, FundService>();
            builder.Services.AddSingleton<ILoanService, LoanService>();
            builder.Services.AddSingleton<IInvestorService, InvestorService>();
            builder.Services.AddSingleton<IInvestorAliasService, InvestorAliasService>();
            builder.Services.AddSingleton<ILoanAliasService, LoanAliasService>();
            builder.Services.AddSingleton<ILoanSecurityValueService, LoanSecurityValueService>();
            builder.Services.AddSingleton<IOtherCostCaptureService, OtherCostCaptureService>();

            builder.Services.Configure<CmhcUploadOptions>(configuration.GetSection(CmhcUploadOptions.SectionName));
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 52_428_800;
            });
            builder.Services.AddScoped<ICmhcFileStorage, LocalCmhcFileStorage>();
            builder.Services.AddScoped<ICmhcUploadService, CmhcUploadService>();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // CORS - allow Angular dev origin; adjust for production
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularDev", policy =>
                    policy
                        .WithOrigins("http://localhost:4200", "https://localhost:4200")
                        .AllowAnyHeader()
                        .AllowAnyMethod());
            });

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<ICmhcFileStorage>().EnsureStorageReady();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowAngularDev");
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
