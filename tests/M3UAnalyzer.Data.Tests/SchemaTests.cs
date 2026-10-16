using M3UAnalyzer.Data.Entities;
using M3UAnalyzer.Data.Tests.TestSupport;
using M3UAnalyzer.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace M3UAnalyzer.Data.Tests;

[TestClass]
public sealed class SchemaTests
{
    [TestMethod]
    public async Task Migrations_CreateExactlyTheExpectedTables()
    {
        await using var database = await TempDatabase.CreateAsync();

        var tables = await database.ColumnAsync("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;");

        CollectionAssert.AreEqual(
            new[] { "CatalogGroups", "Episodes", "ImportWarnings", "ItemGroups", "Items", "SeriesEpisodeLoads", "SnapshotSections", "Snapshots", "Sources", "__EFMigrationsHistory", "__EFMigrationsLock" },
            tables);
    }

    [TestMethod]
    public async Task Model_HasNoPendingChanges()
    {
        await using var database = await TempDatabase.CreateAsync();
        await using var db = database.CreateContext();

        Assert.IsFalse(db.Database.HasPendingModelChanges(), "Model and migrations differ: add a migration in the card that changes the model.");
    }

    [TestMethod]
    public async Task Initialize_EnablesWal()
    {
        await using var database = await TempDatabase.CreateAsync();

        Assert.AreEqual("wal", await database.ScalarAsync("PRAGMA journal_mode;"));
    }

    [TestMethod]
    public async Task Items_HaveLookupIndexes()
    {
        await using var database = await TempDatabase.CreateAsync();

        var indexes = await database.ColumnAsync("SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'Items' AND name LIKE 'IX_%' ORDER BY name;");

        CollectionAssert.AreEqual(
            new[] { "IX_Items_SnapshotId_ContentType", "IX_Items_SnapshotId_ProviderItemId", "IX_Items_SnapshotId_StreamKey", "IX_Items_SnapshotId_TvgId" },
            indexes);
    }

    [TestMethod]
    public async Task Enums_AreStoredAsText()
    {
        await using var database = await TempDatabase.CreateAsync();
        await using (var db = database.CreateContext())
        {
            db.Sources.Add(new SourceEntity
            {
                Id = "11111111-1111-1111-1111-111111111111",
                Name = "Test",
                Kind = SourceKind.Xtream,
                DisplayLocation = "http://example.invalid:8080",
                CreatedUtc = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            });
            await db.SaveChangesAsync();
        }

        Assert.AreEqual("Xtream", await database.ScalarAsync("SELECT Kind FROM Sources;"));
    }

    [TestMethod]
    public async Task ForeignKeys_AreEnforced()
    {
        await using var database = await TempDatabase.CreateAsync();
        await using var db = database.CreateContext();
        db.Snapshots.Add(new SnapshotEntity { Id = "22222222-2222-2222-2222-222222222222", SourceId = "missing-source", StartedUtc = DateTime.UtcNow });

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [TestMethod]
    public void ConnectionString_EnablesForeignKeys()
    {
        var connectionString = AnalyzerDatabase.CreateConnectionString("/tmp/x.db");

        StringAssert.Contains(connectionString, "Foreign Keys=True");
        StringAssert.Contains(connectionString, "Data Source=/tmp/x.db");
    }
}
