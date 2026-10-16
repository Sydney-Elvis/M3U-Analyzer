using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace M3UAnalyzer.Data.Tests.TestSupport;

/// <summary>A migrated SQLite database in a temp file, deleted on dispose.</summary>
public sealed class TempDatabase : IAsyncDisposable
{
    private TempDatabase(string path)
    {
        FilePath = path;
        ConnectionString = AnalyzerDatabase.CreateConnectionString(path);
    }

    public string FilePath { get; }
    public string ConnectionString { get; }

    public static async Task<TempDatabase> CreateAsync()
    {
        var database = new TempDatabase(Path.Combine(Path.GetTempPath(), $"m3ua-test-{Guid.NewGuid():N}.db"));
        await using var db = database.CreateContext();
        await AnalyzerDatabase.InitializeAsync(db, CancellationToken.None);
        return database;
    }

    public AnalyzerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AnalyzerDbContext>().UseSqlite(ConnectionString).Options);

    public async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    public async Task<List<string>> ColumnAsync(string sql)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { FilePath, FilePath + "-wal", FilePath + "-shm" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        return ValueTask.CompletedTask;
    }
}
