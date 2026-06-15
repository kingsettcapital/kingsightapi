namespace kingsightapi.Configuration;

public static class CorsExtensions
{
    public const string PolicyName = "AllowAngularDev";

    public static IServiceCollection AddAngularCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:4200", "https://localhost:4200","https://kingsightdev.kingsettcapital.com"];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
                policy.WithOrigins(corsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        return services;
    }

    public static WebApplication UseAngularCors(this WebApplication app)
    {
        app.UseRouting();
        app.UseCors(PolicyName);
        return app;
    }
}
