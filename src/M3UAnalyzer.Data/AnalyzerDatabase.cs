using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace M3UAnalyzer.Data;

public static class AnalyzerDatabase
{
    public static string CreateConnectionString(string databaseFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseFile);
        return new SqliteConnectionStringBuilder { DataSource = databaseFile, ForeignKeys = true, Pooling = true }.ToString();
    }

    public static async Task InitializeAsync(AnalyzerDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        await db.Database.MigrateAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
    }
}
