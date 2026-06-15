using log4net;

namespace kingsightapi.Configuration;

/// <summary>Creates the logs folder and points log4net at an absolute path under the app base directory.</summary>
public static class Log4NetBootstrap
{
    public static string Configure(WebApplicationBuilder builder)
    {
        // Development: project-root/logs (easy to find in Explorer). Server/publish: app folder/logs.
        var appRoot = builder.Environment.IsDevelopment()
            ? builder.Environment.ContentRootPath
            : AppContext.BaseDirectory;

        var logDirectory = Path.Combine(appRoot, "logs");
        Directory.CreateDirectory(logDirectory);

        GlobalContext.Properties["LogDir"] = logDirectory;

        var log4NetConfigPath = Path.Combine(AppContext.BaseDirectory, "log4net.config");
        if (!File.Exists(log4NetConfigPath))
        {
            log4NetConfigPath = Path.Combine(builder.Environment.ContentRootPath, "log4net.config");
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddLog4Net(log4NetConfigPath);

        return logDirectory;
    }
}
