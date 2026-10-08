using Microsoft.EntityFrameworkCore;

namespace Cms.Studio.Core.Data;

/// <summary>
/// nopCommerce-style data provider selection. Pick the database in appsettings.json:
/// "DataProvider": { "ProviderName": "SqlServer | MySql | PostgreSQL | Sqlite", "ConnectionString": "...",
///                   "CharacterSet": "utf8mb4", "Collation": "utf8mb4_unicode_ci" }
/// </summary>
public class DataProviderSettings
{
    public const string SqlServerProvider = "SqlServer";
    public const string MySqlProvider = "MySql";
    public const string PostgreSqlProvider = "PostgreSQL";
    public const string SqliteProvider = "Sqlite";

    public string ProviderName { get; set; } = SqliteProvider;
    public string ConnectionString { get; set; } = "Data Source=cmsstudio.db";

    /// <summary>Character set used when the bootstrap creates the database (MySQL / PostgreSQL).</summary>
    public string? CharacterSet { get; set; } = "utf8mb4";

    /// <summary>Collation used when the bootstrap creates the database (MySQL / SQL Server / PostgreSQL).</summary>
    public string? Collation { get; set; } = "utf8mb4_unicode_ci";

    /// <summary>Normalized provider kind, so the rest of the code never string-compares provider names.</summary>
    public DataProviderKind ProviderKind => ProviderName.Trim().ToLowerInvariant() switch
    {
        "sqlserver" or "mssql" => DataProviderKind.SqlServer,
        "mysql" or "mariadb" => DataProviderKind.MySql,
        "postgresql" or "postgres" or "npgsql" => DataProviderKind.PostgreSql,
        _ => DataProviderKind.Sqlite
    };

    public void ApplyTo(DbContextOptionsBuilder optionsBuilder)
    {
        switch (ProviderKind)
        {
            case DataProviderKind.SqlServer:
                optionsBuilder.UseSqlServer(ConnectionString);
                break;
            case DataProviderKind.MySql:
                // Oracle's MySql.EntityFrameworkCore provider (Pomelo has no EF Core 10 release yet)
                optionsBuilder.UseMySQL(ConnectionString);
                break;
            case DataProviderKind.PostgreSql:
                optionsBuilder.UseNpgsql(ConnectionString);
                break;
            default:
                optionsBuilder.UseSqlite(ConnectionString);
                break;
        }
    }
}

/// <summary>Database engine behind <see cref="DataProviderSettings.ProviderName"/>.</summary>
public enum DataProviderKind
{
    SqlServer,
    MySql,
    PostgreSql,
    Sqlite
}
