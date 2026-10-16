using M3Undle.Core.Epg;
using M3Undle.Core.Events;
using M3Undle.Core.M3u;
using M3Undle.Core.Net;
using M3Undle.Core.Providers;

namespace M3UAnalyzer.Ingestion.Tests;

/// <summary>
/// Pins the M3Undle.Core behaviors the Analyzer depends on. If one of these fails, the Core checkout is not the
/// pinned commit (see build/m3undle-core.ref) or Core changed behavior. Do not edit expectations: escalate.
/// </summary>
[TestClass]
public sealed class CoreContractTests
{
    private const string Playlist =
        "#EXTM3U\n" +
        "#EXTINF:-1 tvg-id=\"cnn.us\" tvg-name=\"CNN\" group-title=\"News\",CNN HD\n" +
        "http://example.invalid/live/u/p/1.ts\n" +
        "#EXTINF:-1,No Url\n" +
        "#EXTINF:-1 group-title=\"Movies\",Film, The (2020)\n" +
        "http://example.invalid/movie/u/p/2.mkv\n" +
        "#EXTINF:-1 tvg-id=\"\",\n" +
        "#EXTGRP:Sports\n" +
        "http://example.invalid/live/u/p/3.ts\n";

    [TestMethod]
    public void PlaylistParser_KeepsEntriesWithoutUrlAndIgnoresExtGrp()
    {
        var doc = new PlaylistParser().Parse(Playlist);

        Assert.AreEqual(4, doc.Entries.Count);
        Assert.AreEqual("News", doc.Entries[0].Group);
        Assert.AreEqual("CNN HD", doc.Entries[0].Title);
        Assert.IsNull(doc.Entries[1].Url);
        Assert.AreEqual("Film, The (2020)", doc.Entries[2].Title);
        Assert.AreEqual(string.Empty, doc.Entries[3].Title);
        Assert.IsNull(doc.Entries[3].Group);
        Assert.AreEqual(2, doc.Entries[3].MetadataLines.Count);
    }

    [TestMethod]
    public void ProviderChannelNormalizer_ParseEntry_ExtractsAttributes()
    {
        var doc = new PlaylistParser().Parse(Playlist);

        var channel = ProviderChannelNormalizer.ParseEntry(doc.Entries[0]);

        Assert.AreEqual("cnn.us", channel.TvgId);
        Assert.AreEqual("CNN", channel.TvgName);
        Assert.AreEqual("CNN HD", channel.DisplayName);
        Assert.AreEqual("News", channel.GroupTitle);
    }

    [TestMethod]
    public void ProviderChannelNormalizer_ParseEntry_ThrowsWhenUrlMissing()
    {
        var doc = new PlaylistParser().Parse(Playlist);

        Assert.ThrowsExactly<ArgumentException>(() => ProviderChannelNormalizer.ParseEntry(doc.Entries[1]));
    }

    [TestMethod]
    [DataRow("http://example.invalid/live/u/p/123.ts", "xtream:123")]
    [DataRow("http://example.invalid:8080/u/p/123", "xtream:123")]
    [DataRow("http://cdn.example.invalid/hls/stream.m3u8?token=abc&username=u&password=p", "/hls/stream.m3u8?token=abc")]
    public void ProviderChannelNormalizer_BuildStreamLocator_RequiresPinnedCore(string url, string expected)
    {
        // BuildStreamLocator exists from Core 1.0.0-beta.11 (commit b694f5e). A compile error here means the
        // checkout is older than the pinned commit. The token= case shows why the Analyzer stores only a hash.
        Assert.AreEqual(expected, ProviderChannelNormalizer.BuildStreamLocator(url));
    }

    [TestMethod]
    public void ProviderChannelNormalizer_BuildStreamLocator_UsesProviderStreamId()
    {
        Assert.AreEqual("xtream:123", ProviderChannelNormalizer.BuildStreamLocator("x", "123"));
    }

    [TestMethod]
    [DataRow("http://example.invalid/unknown", "live")]
    [DataRow("http://example.invalid/file.mp4", "vod")]
    [DataRow("http://example.invalid/series/u/p/1.mp4", "series")]
    public void LiveClassifier_ClassifyContent_DefaultsToLive(string url, string expected)
    {
        Assert.AreEqual(expected, LiveClassifier.ClassifyContent(url));
    }

    [TestMethod]
    public void XmltvParser_DropsEmptyTitleAndBadTimestampSilently()
    {
        const string xml =
            "<tv><channel id=\"a\"><display-name>A</display-name></channel>" +
            "<programme start=\"20261015100000 +0000\" stop=\"20261015110000 +0000\" channel=\"a\"><title>T</title></programme>" +
            "<programme start=\"20261015110000 +0000\" stop=\"20261015120000 +0000\" channel=\"a\"><title></title></programme>" +
            "<programme start=\"bad\" stop=\"20261015120000 +0000\" channel=\"a\"><title>X</title></programme></tv>";

        var catalogue = new XmltvParser().Parse("s", xml);

        Assert.AreEqual(1, catalogue.Channels.Count);
        Assert.AreEqual(1, catalogue.ProgrammesByChannel["a"].Count);
    }

    [TestMethod]
    public void EpgChannelIndex_MatchesByTvgId()
    {
        var index = new EpgChannelIndex([new EpgChannelRecord("s", "a", "A", null)]);

        var match = index.FindBestMatch(new EpgChannelMatchCandidate("zzz", "a", null));

        Assert.IsNotNull(match);
        Assert.AreEqual("auto_id", match.Mode);
        Assert.AreEqual(1.0f, match.Confidence);
        Assert.IsNull(index.FindBestMatch(new EpgChannelMatchCandidate("zzz", null, null)));
    }

    [TestMethod]
    public void EpgChannelMatcher_NormalizeName()
    {
        Assert.AreEqual("cnn us hd", EpgChannelMatcher.NormalizeName("CNN-US! HD"));
    }

    [TestMethod]
    public void EpgWindowCoverageAnalyzer_Evaluate()
    {
        var now = new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);
        IReadOnlyList<EpgInterval> covered = [new EpgInterval(now, now.AddHours(24))];
        IReadOnlyList<EpgInterval> empty = [];

        var result = EpgWindowCoverageAnalyzer.Evaluate([covered, empty], now, TimeSpan.FromHours(12), TimeSpan.FromMinutes(30));

        Assert.AreEqual(2, result.RelevantChannels);
        Assert.AreEqual(1, result.UsableChannels);
        Assert.AreEqual(1, result.ChannelsWithCurrentOrFutureData);
    }

    [TestMethod]
    public void EventChannelClassifier_DetectsEvent()
    {
        var result = EventChannelClassifier.Classify("NFL 01: Bears vs Packers 8:20 PM ET", "PPV EVENTS");

        Assert.IsTrue(result.IsEvent);
    }

    [TestMethod]
    public void UrlRedactor_KeepsPath_SoAnalyzerMustNotRelyOnIt()
    {
        Assert.AreEqual("http://example.invalid:8080/live/u/p/1.ts", UrlRedactor.RedactUrl("http://u:p@example.invalid:8080/live/u/p/1.ts?x=1#f"));
    }
}