using System.Diagnostics;
using M3UAnalyzer.Data.Snapshots;
using M3UAnalyzer.Data.Tests.TestSupport;
using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;
using Microsoft.EntityFrameworkCore;

namespace M3UAnalyzer.Data.Tests.Snapshots;

[TestClass]
public sealed class SnapshotWriterTests
{
    private static RawCatalogItem Item(int ordinal, ContentType type, string title, params string[] groups) => new()
    {
        Ordinal = ordinal,
        ContentType = type,
        ContentTypeBasis = ContentTypeBasis.UrlInferred,
        RawTitle = title,
        DisplayTitle = title,
        StreamKey = $"key{ordinal:D29}",
        GroupNames = groups,
    };

    [TestMethod]
    public async Task Groups_Items_AndLinks_ArePersisted()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);

        await writer.AddGroupsAsync([new CatalogGroup(ContentType.Live, "News", "1"), new CatalogGroup(ContentType.Live, "Empty Category", "2")], CancellationToken.None);
        await writer.AddItemsAsync(
        [
            Item(0, ContentType.Live, "CNN", "News"),
            Item(1, ContentType.Live, "BBC", "News", "UK"),
            Item(2, ContentType.Live, "Orphan"),
        ], CancellationToken.None);

        await using var db = database.CreateContext();
        Assert.AreEqual(3, await db.Items.CountAsync());
        Assert.AreEqual(3, writer.ItemsWritten);
        var groups = await db.CatalogGroups.OrderBy(g => g.Id).Select(g => $"{g.RawName}:{g.ProviderCategoryId ?? "-"}").ToListAsync();
        CollectionAssert.AreEqual(new[] { "News:1", "Empty Category:2", "UK:-" }, groups, "groups from items are created on demand");
        Assert.AreEqual(3, await db.ItemGroups.CountAsync());
    }

    [TestMethod]
    public async Task ItemFields_RoundTrip_AndEmptyAttributesAreNull()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);

        await writer.AddItemsAsync(
        [
            new RawCatalogItem
            {
                Ordinal = 5, ContentType = ContentType.Vod, ContentTypeBasis = ContentTypeBasis.ProviderDeclared,
                RawTitle = "", DisplayTitle = "(untitled #5)", DisplayTitleIsFallback = true, ProviderItemId = "201",
                StreamKey = "869385665deaed9557198211a604c7d0", TvgId = "t", TvgName = "n", LogoUrl = "http://example.invalid/p.jpg",
                Attributes = new Dictionary<string, string> { ["rating"] = "8.7" }, Year = 1999, ContainerExtension = "mkv",
            },
            new RawCatalogItem { Ordinal = 6, ContentType = ContentType.Unknown, ContentTypeBasis = ContentTypeBasis.Defaulted, RawTitle = "x", DisplayTitle = "x" },
        ], CancellationToken.None);

        await using var db = database.CreateContext();
        var first = await db.Items.SingleAsync(i => i.Ordinal == 5);
        Assert.AreEqual(ContentType.Vod, first.ContentType);
        Assert.AreEqual(ContentTypeBasis.ProviderDeclared, first.ContentTypeBasis);
        Assert.AreEqual(string.Empty, first.RawTitle);
        Assert.IsTrue(first.DisplayTitleIsFallback);
        Assert.AreEqual("201", first.ProviderItemId);
        Assert.AreEqual("{\"rating\":\"8.7\"}", first.AttributesJson);
        Assert.AreEqual(1999, first.Year);
        Assert.AreEqual("mkv", first.ContainerExtension);
        var second = await db.Items.SingleAsync(i => i.Ordinal == 6);
        Assert.IsNull(second.AttributesJson);
        Assert.IsNull(second.StreamKey);
    }

    [TestMethod]
    public async Task SameGroupNameInDifferentContentTypes_AreDistinctGroups()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);

        await writer.AddItemsAsync([Item(0, ContentType.Live, "A", "News"), Item(1, ContentType.Unknown, "B", "News")], CancellationToken.None);

        await using var db = database.CreateContext();
        Assert.AreEqual(2, await db.CatalogGroups.CountAsync());
    }

    [TestMethod]
    public async Task DuplicateGroupNamesOnOneItem_CreateOneLink()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);

        await writer.AddItemsAsync([Item(0, ContentType.Live, "A", "News", "News")], CancellationToken.None);

        await using var db = database.CreateContext();
        Assert.AreEqual(1, await db.ItemGroups.CountAsync());
    }

    [TestMethod]
    public async Task SectionResult_IsUpserted()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);

        await writer.SetSectionResultAsync(new SectionResult(CatalogSection.Vod, SectionStatus.Failed, 0, "HTTP 500"), CancellationToken.None);
        await writer.SetSectionResultAsync(new SectionResult(CatalogSection.Vod, SectionStatus.Ok, 3, null), CancellationToken.None);
        await writer.SetSectionResultAsync(new SectionResult(CatalogSection.Series, SectionStatus.NotRequested, 0, null), CancellationToken.None);

        await using var db = database.CreateContext();
        var sections = await db.SnapshotSections.OrderBy(s => s.Section).Select(s => $"{s.Section}:{s.Status}:{s.ItemCount}:{s.ErrorSummary ?? "-"}").ToListAsync();
        CollectionAssert.AreEqual(new[] { "Series:NotRequested:0:-", "Vod:Ok:3:-" }, sections);
    }

    [TestMethod]
    public async Task Warnings_AreStoredWithSamplesJson()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);

        await writer.AddWarningsAsync([new ImportWarning(CatalogSection.Live, ImportWarningCodes.M3uNoGroup, 4, ["A", "B"])], CancellationToken.None);

        await using var db = database.CreateContext();
        var warning = await db.ImportWarnings.SingleAsync();
        Assert.AreEqual("M3U_NO_GROUP", warning.Code);
        Assert.AreEqual(4, warning.Count);
        Assert.AreEqual("[\"A\",\"B\"]", warning.SamplesJson);
    }

    [TestMethod]
    public async Task EmptyBatches_AreNoOps()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);

        await writer.AddGroupsAsync([], CancellationToken.None);
        await writer.AddItemsAsync([], CancellationToken.None);
        await writer.AddWarningsAsync([], CancellationToken.None);

        Assert.AreEqual(0, writer.ItemsWritten);
    }

    [TestMethod]
    public async Task FiftyThousandItems_AreWrittenWithinThirtySeconds()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);
        var stopwatch = Stopwatch.StartNew();

        for (var batch = 0; batch < 50; batch++)
        {
            var items = Enumerable.Range(batch * 1000, 1000).Select(i => Item(i, ContentType.Live, $"Channel {i}", $"Group {i % 200}")).ToList();
            await writer.AddItemsAsync(items, CancellationToken.None);
        }

        stopwatch.Stop();
        Assert.AreEqual(50_000, writer.ItemsWritten);
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"took {stopwatch.Elapsed}");
        Assert.AreEqual(50_000L, await database.ScalarAsync("SELECT COUNT(*) FROM ItemGroups;"));
        Assert.AreEqual(200L, await database.ScalarAsync("SELECT COUNT(*) FROM CatalogGroups;"));
    }
}
