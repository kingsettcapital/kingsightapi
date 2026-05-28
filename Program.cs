using System.Text.Json;
using kingsightapi.Configuration;
using kingsightapi.Services;
using Microsoft.OpenApi.Models;

namespace kingsightapi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var configuration = builder.Configuration;

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                });

            builder.Services.AddEntraAuthentication(
                configuration,
                allowAnonymousForLocalTesting: builder.Environment.IsDevelopment());

            builder.Services.AddSingleton<IDBService, DBService>();
            builder.Services.AddSingleton<IFundService, FundService>();
            builder.Services.AddSingleton<ILoanService, LoanService>();
            builder.Services.AddSingleton<IInvestorService, InvestorService>();
            builder.Services.AddSingleton<ILoanFormService, LoanFormService>();
            builder.Services.AddSingleton<IInvestorPortalService, InvestorPortalService>();
            builder.Services.AddSingleton<IFundPortalService, FundPortalService>();
            builder.Services.AddSingleton<IPropertyPortalService, PropertyPortalService>();

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
                ?? ["http://localhost:4200", "https://localhost:4200"];
            var isDevelopment = builder.Environment.IsDevelopment();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularDev", policy =>
                {
                    if (isDevelopment)
                    {
                        policy.SetIsOriginAllowed(origin =>
                        {
                            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                            {
                                return false;
                            }

                            return uri.Host is "localhost" or "127.0.0.1";
                        });
                    }
                    else
                    {
                        policy.WithOrigins(corsOrigins);
                    }

                    policy.AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseRouting();
            app.UseCors("AllowAngularDev");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.Run();
        }
    }
}
