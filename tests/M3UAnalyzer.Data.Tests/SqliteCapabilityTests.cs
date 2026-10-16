using Microsoft.Data.Sqlite;

namespace M3UAnalyzer.Data.Tests;

/// <summary>Early detection of SQLite features later cards depend on (FTS5, RETURNING, WAL).</summary>
[TestClass]
public sealed class SqliteCapabilityTests
{
    [TestMethod]
    public void Fts5_IsAvailable_AndSupportsPrefixQueries()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, "CREATE VIRTUAL TABLE t USING fts5(title, tokenize='unicode61 remove_diacritics 2');");
        Execute(connection, "INSERT INTO t(title) VALUES ('US: CNN HD'), ('Télé Québec'), ('HBO East');");

        Assert.AreEqual(1L, Scalar(connection, "SELECT COUNT(*) FROM t WHERE t MATCH '\"cn\"* \"hd\"*';"));
        Assert.AreEqual(1L, Scalar(connection, "SELECT COUNT(*) FROM t WHERE t MATCH '\"tele\"*';"));
    }

    [TestMethod]
    public void InsertReturning_IsAvailable()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, "CREATE TABLE x (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL);");

        var first = Scalar(connection, "INSERT INTO x (Name) VALUES ('a') RETURNING Id;");
        var second = Scalar(connection, "INSERT INTO x (Name) VALUES ('b') RETURNING Id;");

        Assert.AreEqual(1L, first);
        Assert.AreEqual(2L, second);
    }

    [TestMethod]
    public void WalJournalMode_CanBeEnabledOnFileDatabase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"m3ua-wal-{Guid.NewGuid():N}.db");
        try
        {
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA journal_mode=WAL;";
                Assert.AreEqual("wal", (string?)command.ExecuteScalar());
            }
        }
        finally
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }
}