using M3UAnalyzer.Data.Entities;
using M3UAnalyzer.Domain.Imports;
using M3UAnalyzer.Domain.Sources;

namespace M3UAnalyzer.Data.Tests.TestSupport;

/// <summary>Creates the parent rows that catalog rows need.</summary>
public static class Seed
{
    public static readonly DateTime Start = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);

    public static async Task<string> SourceAsync(TempDatabase database, string name = "Test source")
    {
        var id = Guid.NewGuid().ToString("D");
        await using var db = database.CreateContext();
        db.Sources.Add(new SourceEntity { Id = id, Name = name, Kind = SourceKind.M3uUrl, DisplayLocation = "http://example.invalid/list.m3u", CreatedUtc = Start, UpdatedUtc = Start });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<string> SnapshotAsync(TempDatabase database, string sourceId, SnapshotStatus status = SnapshotStatus.Importing, DateTime? startedUtc = null)
    {
        var id = Guid.NewGuid().ToString("D");
        await using var db = database.CreateContext();
        db.Snapshots.Add(new SnapshotEntity { Id = id, SourceId = sourceId, Status = status, StartedUtc = startedUtc ?? Start, RulesVersion = 1 });
        await db.SaveChangesAsync();
        return id;
    }
}
