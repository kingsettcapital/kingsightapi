using System.Text.Json;
using kingsightapi.Services;
using kingsightapi.Entities;

namespace kingsightapi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.WebHost.UseUrls("https://localhost:7140"); // Set the URL for the API

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
            //builder.Services.AddSingleton<ILoanService, LoanService>();
            //builder.Services.AddSingleton<IInvestorService, InvestorService>();
            builder.Services.AddSingleton<IInvestorAliasService, InvestorAliasService>();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // CORS - allow Angular dev origin; adjust for production
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularDev", policy =>
                    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod());
            });

            var app = builder.Build();

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
