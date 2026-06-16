using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace kingsightapi.Configuration;

/// <summary>Sanitizes connection strings and logs API/SQL errors for deployment debugging.</summary>
public static partial class ConnectionLogging
{
    [GeneratedRegex(
        @"(?i)(Password|Pwd|User ID|UID|Access Token|Client Secret)\s*=\s*[^;]*",
        RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveValuePattern();

    public static string Sanitize(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "(empty)";
        }

        return SensitiveValuePattern().Replace(connectionString, "$1=***");
    }

    public static void LogControllerError(
        ILogger logger,
        Exception ex,
        string message,
        params object?[] args)
    {
        if (TryFindSqlException(ex, out var sqlEx))
        {
            var sqlArgs = new object?[args.Length + 3];
            Array.Copy(args, sqlArgs, args.Length);
            sqlArgs[args.Length] = sqlEx.Number;
            sqlArgs[args.Length + 1] = sqlEx.State;
            sqlArgs[args.Length + 2] = sqlEx.Server;

            logger.LogError(
                ex,
                message + " SqlNumber={SqlNumber}, SqlState={SqlState}, SqlServer={SqlServer}",
                sqlArgs);
            return;
        }

        logger.LogError(ex, message, args);
    }

    public static void LogConnectionError(
        ILogger logger,
        Exception ex,
        string operation,
        string? connectionString)
    {
        var connectionInfo = Sanitize(connectionString);

        if (TryFindSqlException(ex, out var sqlEx))
        {
            logger.LogError(
                ex,
                "Fabric SQL error during {Operation}. {ConnectionInfo} SqlNumber={SqlNumber}, SqlState={SqlState}, SqlServer={SqlServer}",
                operation,
                connectionInfo,
                sqlEx.Number,
                sqlEx.State,
                sqlEx.Server);
            return;
        }

        logger.LogError(
            ex,
            "Fabric connection error during {Operation}. {ConnectionInfo}",
            operation,
            connectionInfo);
    }

    private static bool TryFindSqlException(Exception ex, out SqlException sqlException)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                sqlException = sql;
                return true;
            }
        }

        sqlException = null!;
        return false;
    }
}
