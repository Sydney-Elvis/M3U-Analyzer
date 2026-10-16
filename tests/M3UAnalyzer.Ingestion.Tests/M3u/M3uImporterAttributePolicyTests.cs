using System.Text.Json;
using M3UAnalyzer.Domain.Security;
using M3UAnalyzer.Ingestion.M3u;
using M3UAnalyzer.Ingestion.Tests.TestDoubles;

namespace M3UAnalyzer.Ingestion.Tests.M3u;

[TestClass]
public sealed class M3uImporterAttributePolicyTests
{
    // Reproduction from .ai_docs/reviews/P1-05-ingestion-security-review.md (D1), extended with a signed logo and a custom attribute.
    private const string LeakPlaylist =
        "#EXTM3U\n" +
        "#EXTINF:-1 tvg-id=\"ch.1\" tvg-logo=\"http://example.invalid/img/x.png?token=s3cret\" catchup=\"default\" catchup-source=\"http://example.invalid/timeshift/alice/s3cret/{duration}/{start}/1.ts\" group-title=\"G\",Ch\n" +
        "http://example.invalid/live/alice/s3cret/1.ts\n" +
        "#EXTINF:-1 tvg-logo=\"https://cdn.example.invalid/x.png?X-Amz-Signature=s3cret\" x-provider-auth=\"alice:s3cret\" url-tvg=\"http://example.invalid/xmltv.php?username=alice&password=s3cret\" group-title=\"G\",Ch 2\n" +
        "http://example.invalid/live/alice/s3cret/2.ts\n" +
        "#EXTINF:-1 group-title=\"G\",Plain\n" +
        "http://example.invalid/live/alice/s3cret/3.ts\n";

    private static readonly string DefectsPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "m3u", "defects-small.m3u");

    [TestMethod]
    public async Task CredentialBearingAttributes_NeverReachTheSink()
    {
        var sink = new RecordingCatalogSink();

        await new M3uImporter().ImportAsync(LeakPlaylist, sink, CancellationToken.None);

        var json = JsonSerializer.Serialize(sink.Items) + JsonSerializer.Serialize(sink.Warnings) + JsonSerializer.Serialize(sink.Groups);
        Assert.IsFalse(json.Contains("s3cret", StringComparison.Ordinal), "password leaked");
        Assert.IsFalse(json.Contains("alice", StringComparison.Ordinal), "username leaked");
        Assert.IsFalse(json.Contains("timeshift", StringComparison.Ordinal), "catch-up URL template leaked");
        Assert.IsFalse(json.Contains("X-Amz", StringComparison.Ordinal), "signed URL leaked");
    }

    [TestMethod]
    public async Task WithheldKeys_AreRecorded_AndSafeMetadataIsKept()
    {
        var sink = new RecordingCatalogSink();

        await new M3uImporter().ImportAsync(LeakPlaylist, sink, CancellationToken.None);
        var items = sink.Items;

        CollectionAssert.AreEquivalent(new[] { "tvg-id", "catchup", "group-title" }, items[0].Attributes.Keys.ToArray());
        Assert.AreEqual("default", items[0].Attributes["catchup"]);
        CollectionAssert.AreEqual(new[] { "catchup-source" }, items[0].WithheldAttributeKeys.ToArray());
        Assert.AreEqual("http://example.invalid/img/x.png", items[0].LogoUrl, "the logo stays available without its query");
        Assert.AreEqual("https://cdn.example.invalid/x.png", items[1].LogoUrl, "signed-URL parameters are removed");

        CollectionAssert.AreEqual(new[] { "url-tvg", "x-provider-auth" }, items[1].WithheldAttributeKeys.ToArray());
        Assert.IsFalse(items[1].Attributes.ContainsKey("tvg-logo"));

        CollectionAssert.AreEquivalent(new[] { "group-title" }, items[2].Attributes.Keys.ToArray());
        Assert.AreEqual(0, items[2].WithheldAttributeKeys.Count, "nothing withheld is different from withheld");
    }

    [TestMethod]
    public async Task EveryImportedItem_PassesThePersistenceCheck()
    {
        foreach (var content in new[] { LeakPlaylist, await File.ReadAllTextAsync(DefectsPath) })
        {
            var sink = new RecordingCatalogSink();
            await new M3uImporter().ImportAsync(content, sink, CancellationToken.None);

            foreach (var item in sink.Items)
            {
                CatalogAttributePolicy.EnsurePersistable(item);
            }
        }
    }

    [TestMethod]
    public async Task DefectsFixture_WithholdsNothing()
    {
        var sink = new RecordingCatalogSink();

        await new M3uImporter().ImportAsync(await File.ReadAllTextAsync(DefectsPath), sink, CancellationToken.None);

        Assert.IsTrue(sink.Items.All(static i => i.WithheldAttributeKeys.Count == 0));
        Assert.IsTrue(sink.Items.All(static i => !i.Attributes.ContainsKey("tvg-logo")), "tvg-logo is promoted to LogoUrl");
    }
}
