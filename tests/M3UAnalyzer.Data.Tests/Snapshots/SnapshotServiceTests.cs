using M3UAnalyzer.Data.Entities;
using M3UAnalyzer.Data.Snapshots;
using M3UAnalyzer.Data.Tests.TestSupport;
using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace M3UAnalyzer.Data.Tests.Snapshots;

[TestClass]
public sealed class SnapshotServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    private static SnapshotSectionEntity S(CatalogSection section, SectionStatus status) => new() { Section = section, Status = status };

    [TestMethod]
    [DataRow(SectionStatus.Ok, SectionStatus.Ok, SectionStatus.Ok, SnapshotStatus.Complete)]
    [DataRow(SectionStatus.Ok, SectionStatus.NotRequested, SectionStatus.NotRequested, SnapshotStatus.Complete)]
    [DataRow(SectionStatus.Ok, SectionStatus.Failed, SectionStatus.NotRequested, SnapshotStatus.Partial)]
    [DataRow(SectionStatus.Failed, SectionStatus.Ok, SectionStatus.Ok, SnapshotStatus.Partial)]
    [DataRow(SectionStatus.Failed, SectionStatus.Failed, SectionStatus.NotRequested, SnapshotStatus.Failed)]
    [DataRow(SectionStatus.NotRequested, SectionStatus.NotRequested, SectionStatus.NotRequested, SnapshotStatus.Failed)]
    public void DecideStatus_FromCatalogSections(SectionStatus live, SectionStatus vod, SectionStatus series, SnapshotStatus expected)
    {
        var sections = new[] { S(CatalogSection.Live, live), S(CatalogSection.Vod, vod), S(CatalogSection.Series, series) };

        Assert.AreEqual(expected, SnapshotService.DecideStatus(sections, new SnapshotFinish()));
    }

    [TestMethod]
    public void DecideStatus_IgnoresEpgAndEpisodeSections()
    {
        var sections = new[] { S(CatalogSection.Live, SectionStatus.Ok), S(CatalogSection.Epg, SectionStatus.Failed), S(CatalogSection.Episodes, SectionStatus.Failed) };

        Assert.AreEqual(SnapshotStatus.Complete, SnapshotService.DecideStatus(sections, new SnapshotFinish()));
    }

    [TestMethod]
    public void DecideStatus_CancelledAndFatal_WinOverSections()
    {
        var sections = new[] { S(CatalogSection.Live, SectionStatus.Ok) };

        Assert.AreEqual(SnapshotStatus.Cancelled, SnapshotService.DecideStatus(sections, new SnapshotFinish(Cancelled: true)));
        Assert.AreEqual(SnapshotStatus.Failed, SnapshotService.DecideStatus(sections, new SnapshotFinish(FatalError: "boom")));
    }

    [TestMethod]
    public async Task Complete_BecomesCurrent_AndGroupCountsAreComputed()
    {
        await using var database = await TempDatabase.CreateAsync();
        var sourceId = await Seed.SourceAsync(database);
        var service = new SnapshotService(new TestContextFactory(database), new FixedTimeProvider(T0));

        var snapshotId = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 3);
        var status = await service.FinishAsync(snapshotId, new SnapshotFinish(BytesFetched: 1234), CancellationToken.None);

        Assert.AreEqual(SnapshotStatus.Complete, status);
        await using var db = database.CreateContext();
        Assert.AreEqual(snapshotId, (await db.Sources.SingleAsync()).CurrentSnapshotId);
        var snapshot = await db.Snapshots.SingleAsync();
        Assert.AreEqual(1234L, snapshot.BytesFetched);
        Assert.IsNotNull(snapshot.FinishedUtc);
        Assert.AreEqual(3, (await db.CatalogGroups.SingleAsync(g => g.RawName == "News")).ItemCount);
        Assert.AreEqual(0, (await db.CatalogGroups.SingleAsync(g => g.RawName == "Empty")).ItemCount);
    }

    [TestMethod]
    public async Task FailedImport_KeepsPreviousCurrent_AndRemovesItsCatalogRows()
    {
        await using var database = await TempDatabase.CreateAsync();
        var sourceId = await Seed.SourceAsync(database);
        var service = new SnapshotService(new TestContextFactory(database), new FixedTimeProvider(T0));
        var good = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 2);
        await service.FinishAsync(good, new SnapshotFinish(), CancellationToken.None);

        var bad = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 5);
        var status = await service.FinishAsync(bad, new SnapshotFinish(FatalError: "Playlist fetch failed: http://example.invalid/get.php?username=alice&password=s3cret"), CancellationToken.None);

        Assert.AreEqual(SnapshotStatus.Failed, status);
        await using var db = database.CreateContext();
        Assert.AreEqual(good, (await db.Sources.SingleAsync()).CurrentSnapshotId);
        Assert.AreEqual(0, await db.Items.CountAsync(i => i.SnapshotId == bad));
        Assert.AreEqual(2, await db.Items.CountAsync(i => i.SnapshotId == good));
        var error = (await db.Snapshots.SingleAsync(s => s.Id == bad)).ErrorSummary!;
        Assert.IsFalse(error.Contains("s3cret", StringComparison.Ordinal), error);
        Assert.AreEqual(1, await db.SnapshotSections.CountAsync(s => s.SnapshotId == bad), "sections are kept as history");
    }

    [TestMethod]
    public async Task Partial_DoesNotReplaceComplete_ButReplacesPartialOrNothing()
    {
        await using var database = await TempDatabase.CreateAsync();
        var sourceId = await Seed.SourceAsync(database);
        var service = new SnapshotService(new TestContextFactory(database), new FixedTimeProvider(T0));

        var partial1 = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 1, vod: SectionStatus.Failed);
        Assert.AreEqual(SnapshotStatus.Partial, await service.FinishAsync(partial1, new SnapshotFinish(), CancellationToken.None));
        Assert.AreEqual(partial1, await CurrentAsync(database));

        var partial2 = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 1, vod: SectionStatus.Failed);
        await service.FinishAsync(partial2, new SnapshotFinish(), CancellationToken.None);
        Assert.AreEqual(partial2, await CurrentAsync(database));

        var complete = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 1);
        await service.FinishAsync(complete, new SnapshotFinish(), CancellationToken.None);
        Assert.AreEqual(complete, await CurrentAsync(database));

        var partial3 = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 1, vod: SectionStatus.Failed);
        await service.FinishAsync(partial3, new SnapshotFinish(), CancellationToken.None);
        Assert.AreEqual(complete, await CurrentAsync(database), "a partial import never replaces a complete one");
    }

    [TestMethod]
    public async Task Cancelled_IsRecorded_AndRowsRemoved()
    {
        await using var database = await TempDatabase.CreateAsync();
        var sourceId = await Seed.SourceAsync(database);
        var service = new SnapshotService(new TestContextFactory(database), new FixedTimeProvider(T0));
        var id = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 2);

        Assert.AreEqual(SnapshotStatus.Cancelled, await service.FinishAsync(id, new SnapshotFinish(Cancelled: true), CancellationToken.None));

        await using var db = database.CreateContext();
        Assert.AreEqual(0, await db.Items.CountAsync());
        Assert.IsNull((await db.Sources.SingleAsync()).CurrentSnapshotId);
        Assert.AreEqual("Cancelled by user.", (await db.Snapshots.SingleAsync()).ErrorSummary);
    }

    [TestMethod]
    public async Task Finish_Twice_Throws()
    {
        await using var database = await TempDatabase.CreateAsync();
        var sourceId = await Seed.SourceAsync(database);
        var service = new SnapshotService(new TestContextFactory(database), new FixedTimeProvider(T0));
        var id = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 1);
        await service.FinishAsync(id, new SnapshotFinish(), CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.FinishAsync(id, new SnapshotFinish(), CancellationToken.None));
    }

    [TestMethod]
    public async Task Begin_UnknownSource_Throws()
    {
        await using var database = await TempDatabase.CreateAsync();
        var service = new SnapshotService(new TestContextFactory(database), new FixedTimeProvider(T0));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.BeginAsync("nope", null, 1, CancellationToken.None));
    }

    [TestMethod]
    public async Task RecoverInterrupted_FailsImportingSnapshots_AndRemovesRows()
    {
        await using var database = await TempDatabase.CreateAsync();
        var sourceId = await Seed.SourceAsync(database);
        var service = new SnapshotService(new TestContextFactory(database), new FixedTimeProvider(T0));
        var id = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 4);

        Assert.AreEqual(1, await service.RecoverInterruptedAsync(CancellationToken.None));

        await using var db = database.CreateContext();
        var snapshot = await db.Snapshots.SingleAsync(s => s.Id == id);
        Assert.AreEqual(SnapshotStatus.Failed, snapshot.Status);
        Assert.AreEqual(SnapshotService.InterruptedMessage, snapshot.ErrorSummary);
        Assert.AreEqual(0, await db.Items.CountAsync());
        Assert.AreEqual(0, await service.RecoverInterruptedAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task Retention_KeepsCurrentPlusNewestSuccessful()
    {
        await using var database = await TempDatabase.CreateAsync();
        var sourceId = await Seed.SourceAsync(database);
        var clock = new FixedTimeProvider(T0);
        var service = new SnapshotService(new TestContextFactory(database), clock);
        var ids = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            clock.Now = T0.AddHours(i);
            var id = await ImportAsync(database, service, sourceId, SectionStatus.Ok, itemCount: 1);
            await service.FinishAsync(id, new SnapshotFinish(), CancellationToken.None);
            ids.Add(id);
        }

        var deleted = await service.ApplyRetentionAsync(sourceId, keepSuccessful: 3, CancellationToken.None);

        Assert.AreEqual(2, deleted);
        await using var db = database.CreateContext();
        var remaining = await db.Snapshots.OrderBy(s => s.StartedUtc).Select(s => s.Id).ToListAsync();
        CollectionAssert.AreEqual(ids.Skip(2).ToArray(), remaining.ToArray(), "current (newest) + 3 newer others are kept");
        Assert.AreEqual(4, await db.Items.CountAsync());
    }

    private static async Task<string> CurrentAsync(TempDatabase database)
    {
        await using var db = database.CreateContext();
        return (await db.Sources.SingleAsync()).CurrentSnapshotId!;
    }

    private static async Task<string> ImportAsync(TempDatabase database, SnapshotService service, string sourceId, SectionStatus live, int itemCount, SectionStatus vod = SectionStatus.NotRequested)
    {
        var id = await service.BeginAsync(sourceId, "b694f5e", 1, CancellationToken.None);
        var writer = new SnapshotWriter(database.ConnectionString, id);
        await writer.AddGroupsAsync([new CatalogGroup(ContentType.Live, "News", null), new CatalogGroup(ContentType.Live, "Empty", "9")], CancellationToken.None);
        await writer.AddItemsAsync(
            Enumerable.Range(0, itemCount).Select(i => new RawCatalogItem
            {
                Ordinal = i, ContentType = ContentType.Live, ContentTypeBasis = ContentTypeBasis.UrlInferred,
                RawTitle = $"C{i}", DisplayTitle = $"C{i}", GroupNames = ["News"],
            }).ToList(),
            CancellationToken.None);
        await writer.SetSectionResultAsync(new SectionResult(CatalogSection.Live, live, itemCount, null), CancellationToken.None);
        if (vod != SectionStatus.NotRequested)
        {
            await writer.SetSectionResultAsync(new SectionResult(CatalogSection.Vod, vod, 0, "HTTP 500"), CancellationToken.None);
        }

        return id;
    }
}
