using System.Text.Json;
using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;
using M3UAnalyzer.Ingestion.M3u;
using M3UAnalyzer.Ingestion.Tests.TestDoubles;

namespace M3UAnalyzer.Ingestion.Tests.M3u;

[TestClass]
public sealed class M3uImporterTests
{
    private static readonly string DefectsPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "m3u", "defects-small.m3u");

    private static async Task<(RecordingCatalogSink Sink, M3uImportSummary Summary)> ImportDefectsAsync()
    {
        var sink = new RecordingCatalogSink();
        var summary = await new M3uImporter().ImportAsync(await File.ReadAllTextAsync(DefectsPath), sink, CancellationToken.None);
        return (sink, summary);
    }

    [TestMethod]
    public async Task Summary_MatchesFixtureReadme()
    {
        var (_, summary) = await ImportDefectsAsync();

        Assert.AreEqual(new M3uImportSummary(Entries: 20, Live: 14, Vod: 3, Series: 2, Unknown: 1, Groups: 9, WarningCount: 6), summary);
    }

    [TestMethod]
    public async Task Sink_IsCalledInOrder_GroupsItemsSectionsWarnings()
    {
        var (sink, _) = await ImportDefectsAsync();

        CollectionAssert.AreEqual(
            new[] { "AddGroupsAsync", "AddItemsAsync", "SetSectionResultAsync", "SetSectionResultAsync", "SetSectionResultAsync", "SetSectionResultAsync", "AddWarningsAsync" },
            sink.Calls);
    }

    [TestMethod]
    public async Task Sections_AreOkForPlaylistTypes_AndEpisodesNotSupported()
    {
        var (sink, _) = await ImportDefectsAsync();

        Assert.AreEqual(new SectionResult(CatalogSection.Live, SectionStatus.Ok, 14, null), sink.Sections[CatalogSection.Live]);
        Assert.AreEqual(new SectionResult(CatalogSection.Vod, SectionStatus.Ok, 3, null), sink.Sections[CatalogSection.Vod]);
        Assert.AreEqual(new SectionResult(CatalogSection.Series, SectionStatus.Ok, 2, null), sink.Sections[CatalogSection.Series]);
        Assert.AreEqual(new SectionResult(CatalogSection.Episodes, SectionStatus.NotSupported, 0, null), sink.Sections[CatalogSection.Episodes]);
        Assert.IsFalse(sink.Sections.ContainsKey(CatalogSection.Epg));
    }

    [TestMethod]
    public async Task Groups_AreDistinctByTypeAndName_InFirstSeenOrder()
    {
        var (sink, _) = await ImportDefectsAsync();

        var actual = sink.Groups.Select(static g => $"{g.ContentType}:{g.RawName}:{g.ProviderCategoryId ?? "-"}").ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "Live:News:-", "Unknown:News:-", "Live:Sports:-", "Vod:Movies | Action:-", "Vod:Movies | Drama:-",
                "Series:Series | Drama:-", "Live:Local:-", "Live:Movies Channels:-", "Live:PPV EVENTS:-",
            },
            actual);
    }

    [TestMethod]
    public async Task Warnings_AreAggregatedWithSamples()
    {
        var (sink, _) = await ImportDefectsAsync();

        var actual = sink.Warnings.Select(static w => $"{w.Section}:{w.Code}:{w.Count}:{string.Join("|", w.Samples)}").ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "Live:M3U_EMPTY_TITLE:2:Empty Title Fallback|(untitled #8)",
                "Live:M3U_MISSING_URL:1:No URL Channel",
                "Live:M3U_NO_GROUP:1:Orphan Channel",
                "Live:M3U_TYPE_DEFAULTED:2:Local Access|HBO East",
            },
            actual);
    }

    [TestMethod]
    public async Task Items_KeepOrdinalsAndRawFacts()
    {
        var (sink, _) = await ImportDefectsAsync();
        var items = sink.Items;

        Assert.AreEqual(20, items.Count);
        CollectionAssert.AreEqual(Enumerable.Range(0, 20).ToArray(), items.Select(static i => i.Ordinal).ToArray());

        var cnn = items[0];
        Assert.AreEqual("US: CNN HD", cnn.RawTitle);
        Assert.AreEqual("US: CNN HD", cnn.DisplayTitle);
        Assert.AreEqual(ContentType.Live, cnn.ContentType);
        Assert.AreEqual(ContentTypeBasis.UrlInferred, cnn.ContentTypeBasis);
        Assert.AreEqual("cnn.us", cnn.TvgId);
        Assert.AreEqual("CNN", cnn.TvgName);
        Assert.AreEqual("http://example.invalid/logos/cnn.png", cnn.LogoUrl);
        Assert.AreEqual("3ba6f1bccd1eaffe17ec2e1f42443f9f", cnn.StreamKey);
        CollectionAssert.AreEqual(new[] { "News" }, cnn.GroupNames.ToArray());
        Assert.IsNull(cnn.ProviderItemId);
        Assert.IsNull(cnn.Year);
    }

    [TestMethod]
    public async Task Items_HandleEachDefect()
    {
        var (sink, _) = await ImportDefectsAsync();
        var items = sink.Items;

        Assert.AreEqual(items[0].StreamKey, items[2].StreamKey, "exact duplicate line shares the stream key");
        Assert.AreEqual(18, items.Where(static i => i.StreamKey is not null).Select(static i => i.StreamKey).Distinct().Count());

        Assert.AreEqual(ContentType.Unknown, items[3].ContentType, "missing URL");
        Assert.AreEqual(ContentTypeBasis.Defaulted, items[3].ContentTypeBasis);
        Assert.IsNull(items[3].StreamKey);
        Assert.AreEqual("missing.url", items[3].TvgId);

        Assert.AreEqual("ESPN", items[4].DisplayTitle, "comma inside quoted attribute");
        Assert.AreEqual("ESPN, Inc", items[4].TvgName);

        Assert.AreEqual(0, items[5].GroupNames.Count, "no group");
        CollectionAssert.AreEqual(new[] { "Sports" }, items[6].GroupNames.ToArray(), "#EXTGRP fallback");

        Assert.AreEqual(string.Empty, items[7].RawTitle);
        Assert.AreEqual("Empty Title Fallback", items[7].DisplayTitle);
        Assert.IsTrue(items[7].DisplayTitleIsFallback);
        Assert.AreEqual("(untitled #8)", items[8].DisplayTitle);
        Assert.IsTrue(items[8].DisplayTitleIsFallback);

        Assert.AreEqual("Good, the Bad and the Ugly, The (1966)", items[10].RawTitle);
        Assert.AreEqual(ContentType.Series, items[11].ContentType);
        Assert.AreEqual(ContentTypeBasis.Defaulted, items[13].ContentTypeBasis);
        Assert.AreEqual(ContentTypeBasis.Defaulted, items[14].ContentTypeBasis);
        Assert.AreEqual("CA", items[16].Attributes["tvg-country"]);
        Assert.AreEqual("7", items[16].Attributes["catchup-days"]);
        Assert.AreEqual("upper.1", items[18].TvgId);
        Assert.AreEqual(ContentType.Vod, items[19].ContentType);
    }

    [TestMethod]
    public async Task NoItem_ContainsCredentialsOrPlayableUrls()
    {
        var (sink, _) = await ImportDefectsAsync();

        var json = JsonSerializer.Serialize(sink.Items) + JsonSerializer.Serialize(sink.Warnings);

        Assert.IsFalse(json.Contains("s3cret", StringComparison.Ordinal), "password leaked");
        Assert.IsFalse(json.Contains("alice", StringComparison.Ordinal), "username leaked");
        Assert.IsFalse(json.Contains("abc123", StringComparison.Ordinal), "signed token leaked");
        Assert.IsFalse(json.Contains("/live/", StringComparison.Ordinal), "stream URL leaked");
    }

    [TestMethod]
    public async Task LargePlaylist_IsSentInBatchesOf1000()
    {
        var lines = new List<string> { "#EXTM3U" };
        for (var i = 0; i < 2500; i++)
        {
            lines.Add($"#EXTINF:-1 group-title=\"G{i % 7}\",Channel {i}");
            lines.Add($"http://example.invalid/live/u/p/{i}.ts");
        }

        var sink = new RecordingCatalogSink();
        var summary = await new M3uImporter().ImportAsync(string.Join('\n', lines), sink, CancellationToken.None);

        Assert.AreEqual(2500, summary.Live);
        CollectionAssert.AreEqual(new[] { 1000, 1000, 500 }, sink.ItemBatchSizes);
        Assert.AreEqual(0, summary.WarningCount);
        Assert.AreEqual(0, sink.Warnings.Count);
    }

    [TestMethod]
    public async Task EmptyPlaylist_ReportsZeroWithOkSections()
    {
        var sink = new RecordingCatalogSink();

        var summary = await new M3uImporter().ImportAsync("#EXTM3U\n", sink, CancellationToken.None);

        Assert.AreEqual(new M3uImportSummary(0, 0, 0, 0, 0, 0, 0), summary);
        Assert.AreEqual(SectionStatus.Ok, sink.Sections[CatalogSection.Live].Status);
        Assert.AreEqual(0, sink.ItemBatchSizes.Count);
    }

    [TestMethod]
    public async Task CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => new M3uImporter().ImportAsync(File.ReadAllText(DefectsPath), new RecordingCatalogSink(), cts.Token));
    }
}
