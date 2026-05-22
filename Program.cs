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

            // Validates Bearer tokens issued by Entra for this API (MSAL acquires them on the Angular app).
            builder.Services.AddEntraAuthentication(configuration);

            builder.Services.AddSingleton<IDBService, DBService>();
            builder.Services.AddSingleton<IFundService, FundService>();
            builder.Services.AddSingleton<ILoanService, LoanService>();
            builder.Services.AddSingleton<IInvestorService, InvestorService>();

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

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowAngularDev");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.Run();
        }
    }
}
