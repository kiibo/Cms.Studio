using Microsoft.EntityFrameworkCore;

namespace Cms.Studio.Core.Data;

/// <summary>
/// nopCommerce-style data provider selection. Pick the database in appsettings.json:
/// "DataProvider": { "ProviderName": "SqlServer | MySql | PostgreSQL | Sqlite", "ConnectionString": "..." }
/// </summary>
public class DataProviderSettings
{
    public const string SqlServerProvider = "SqlServer";
    public const string MySqlProvider = "MySql";
    public const string PostgreSqlProvider = "PostgreSQL";
    public const string SqliteProvider = "Sqlite";

    public string ProviderName { get; set; } = SqliteProvider;
    public string ConnectionString { get; set; } = "Data Source=cmsstudio.db";

    public void ApplyTo(DbContextOptionsBuilder optionsBuilder)
    {
        switch (ProviderName.Trim().ToLowerInvariant())
        {
            case "sqlserver":
                optionsBuilder.UseSqlServer(ConnectionString);
                break;
            case "mysql":
                // Oracle's MySql.EntityFrameworkCore provider (Pomelo has no EF Core 10 release yet)
                optionsBuilder.UseMySQL(ConnectionString);
                break;
            case "postgresql":
            case "postgres":
                optionsBuilder.UseNpgsql(ConnectionString);
                break;
            default:
                optionsBuilder.UseSqlite(ConnectionString);
                break;
        }
    }
}
