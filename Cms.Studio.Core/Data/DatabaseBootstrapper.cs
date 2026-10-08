using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Cms.Studio.Core.Data;

/// <summary>
/// nopCommerce-style database bootstrap (<c>MySqlNopDataProvider.CreateDatabase</c>).
///
/// Before any schema work runs, make sure the database named in the connection string
/// actually exists: connect to the engine's maintenance database ("master" for SQL Server,
/// "postgres" for PostgreSQL, no default database for MySQL), create ours when it is
/// missing — including the character set / collation from appsettings.json — and then wait
/// until the new database accepts connections (on slow hosting it can lag a second behind
/// its own CREATE).
///
/// Writing the table structure is the job of <see cref="DbInitializer"/> (EnsureCreated
/// plus the non-destructive schema reconciliation); this class only creates the database.
/// </summary>
public static class DatabaseBootstrapper
{
    /// <summary>Creates the configured database when it does not exist yet. Server engines
    /// only — SQLite creates its file on first open and needs no bootstrap.</summary>
    /// <param name="db">Context whose provider supplies the ADO.NET connection type.</param>
    /// <param name="settings">Provider selection from appsettings.json.</param>
    /// <param name="triesToConnect">How often to check that the new database became reachable (1s apart).</param>
    public static void CreateDatabaseIfNotExists(CmsDbContext db, DataProviderSettings settings, int triesToConnect = 10)
    {
        if (settings.ProviderKind == DataProviderKind.Sqlite)
            return;

        var template = db.Database.GetDbConnection();
        var (databaseName, serverConnectionString) = SplitServerConnection(settings.ConnectionString, settings.ProviderKind);
        if (string.IsNullOrWhiteSpace(databaseName))
            return; // no default database configured - nothing to create

        if (DatabaseExists(template, settings.ConnectionString))
            return;

        CreateDatabase(settings, template, databaseName!, serverConnectionString);

        // Sometimes on slow servers (hosting) there is a delay before the fresh database
        // accepts connections, while we are already about to write the tables. Retry a few
        // times instead of failing the very first schema write (nopCommerce-style).
        for (var i = 0; i <= triesToConnect; i++)
        {
            if (i == triesToConnect)
                throw new InvalidOperationException(
                    $"Unable to connect to the new database '{databaseName}'. Please try one more time.");

            if (!DatabaseExists(template, settings.ConnectionString))
                Thread.Sleep(1000);
            else
                break;
        }
    }

    /// <summary>Checks whether the database in the connection string accepts connections.</summary>
    public static bool DatabaseExists(DbConnection template, string connectionString)
    {
        try
        {
            using var connection = CreateConnection(template, connectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ---- CREATE DATABASE per provider -----------------------------------------
    private static void CreateDatabase(DataProviderSettings settings, DbConnection template,
        string databaseName, string serverConnectionString)
    {
        var query = settings.ProviderKind switch
        {
            // IF NOT EXISTS keeps this idempotent when the probe raced a parallel startup.
            DataProviderKind.MySql =>
                $"CREATE DATABASE IF NOT EXISTS {QuoteMySql(databaseName)}{MySqlCharsetClause(settings)}",
            DataProviderKind.SqlServer =>
                $"IF DB_ID(N'{EscapeLiteral(databaseName)}') IS NULL " +
                $"CREATE DATABASE {QuoteSqlServer(databaseName)}{SqlServerCollationClause(settings)}",
            DataProviderKind.PostgreSql =>
                $"CREATE DATABASE {QuotePostgreSql(databaseName)}{PostgreSqlOptionsClause(settings)}",
            _ => throw new NotSupportedException($"Cannot create databases for provider '{settings.ProviderName}'.")
        };

        try
        {
            using var connection = CreateConnection(template, serverConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create the database '{databaseName}'. " +
                "Check ConnectionString (server reachable, credentials valid) and that the account may " +
                "create databases, or create the database manually. " +
                $"[{ex.Message}]", ex);
        }
    }

    private static string MySqlCharsetClause(DataProviderSettings settings)
    {
        var clause = string.Empty;
        if (IsSafeToken(settings.CharacterSet))
            clause += $" CHARACTER SET {settings.CharacterSet}";
        if (IsSafeToken(settings.Collation))
            clause += $" COLLATE {settings.Collation}";
        return clause;
    }

    private static string SqlServerCollationClause(DataProviderSettings settings)
        => IsSafeToken(settings.Collation) ? $" COLLATE {settings.Collation}" : string.Empty;

    private static string PostgreSqlOptionsClause(DataProviderSettings settings)
    {
        // Same shape as nopCommerce: with TEMPLATE template0 a non-default encoding /
        // collation can be chosen. The connecting user owns the new database by default.
        var clause = string.Empty;
        if (!string.IsNullOrWhiteSpace(settings.CharacterSet))
            clause += $" ENCODING '{EscapeLiteral(settings.CharacterSet)}'";
        if (!string.IsNullOrWhiteSpace(settings.Collation))
            clause += $" LC_COLLATE '{EscapeLiteral(settings.Collation)}' LC_CTYPE '{EscapeLiteral(settings.Collation)}'";
        return clause.Length == 0 ? string.Empty : $" WITH{clause} TEMPLATE template0";
    }

    /// <summary>Only plain identifiers may be interpolated into DDL (charset / collation names).</summary>
    private static bool IsSafeToken(string? value)
        => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, "^[A-Za-z0-9_]+$");

    // ---- connection string surgery -------------------------------------------
    /// <summary>Splits a connection string into its database name and a maintenance-level
    /// connection string pointing at the engine instead of at our database.</summary>
    private static (string? DatabaseName, string ServerConnectionString) SplitServerConnection(
        string connectionString, DataProviderKind kind)
    {
        var values = new DbConnectionStringBuilder { ConnectionString = connectionString };

        string? databaseName = null;
        var databaseKeys = new List<string>();
        foreach (string key in values.Keys)
        {
            if (!IsDatabaseKey(key)) continue;
            databaseKeys.Add(key);
            if (databaseName is null && values[key] is not null && !string.IsNullOrWhiteSpace(values[key]?.ToString()))
                databaseName = values[key]?.ToString();
        }
        foreach (var key in databaseKeys)
            values.Remove(key);

        // MySQL accepts a connection without a default database; SQL Server and PostgreSQL
        // need their maintenance database instead ("master" / "postgres").
        switch (kind)
        {
            case DataProviderKind.SqlServer:
                values["Initial Catalog"] = "master";
                break;
            case DataProviderKind.PostgreSql:
                values["Database"] = "postgres";
                break;
        }

        return (databaseName, values.ConnectionString);
    }

    /// <summary>The keys that name the default database across the four providers.</summary>
    private static bool IsDatabaseKey(string key)
        => key.Equals("Database", StringComparison.OrdinalIgnoreCase)
           || key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase)
           || key.Equals("DefaultDatabase", StringComparison.OrdinalIgnoreCase);

    /// <summary>Creates a fresh connection of the same provider type as the EF one, so the
    /// bootstrap stays free of hard references to vendor namespaces.</summary>
    private static DbConnection CreateConnection(DbConnection template, string connectionString)
    {
        if (Activator.CreateInstance(template.GetType()) is not DbConnection connection)
            throw new NotSupportedException($"Cannot create a connection of type '{template.GetType().Name}'.");
        connection.ConnectionString = connectionString;
        return connection;
    }

    // ---- identifier / literal quoting ----------------------------------------
    private static string QuoteMySql(string identifier) => "`" + identifier.Replace("`", "``") + "`";
    private static string QuoteSqlServer(string identifier) => "[" + identifier.Replace("]", "]]") + "]";
    private static string QuotePostgreSql(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
    private static string EscapeLiteral(string value) => value.Replace("'", "''");
}
