using System.Text.Json;
using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;
using M3UAnalyzer.Domain.Sources;
using M3UAnalyzer.Ingestion.Fetching;
using M3UAnalyzer.Ingestion.Tests.TestDoubles;
using M3UAnalyzer.Ingestion.Xtream;

namespace M3UAnalyzer.Ingestion.Tests.Xtream;

[TestClass]
public sealed class XtreamImporterTests
{
    private static readonly XtreamCredentials Credentials = new("http://example.invalid:8080", "alice", "s3cret");

    private static async Task<(RecordingCatalogSink Sink, XtreamImportSummary Summary)> RunAsync(FixtureXtreamApiClient client, bool vod = true, bool series = true)
    {
        var sink = new RecordingCatalogSink();
        var summary = await new XtreamImporter(client).ImportAsync(Credentials, new XtreamImportOptions(vod, series), FetchPolicy.Default, sink, CancellationToken.None);
        return (sink, summary);
    }

    [TestMethod]
    public async Task FullImport_ReportsCountsAndSections()
    {
        var (sink, summary) = await RunAsync(new FixtureXtreamApiClient());

        Assert.AreEqual(new XtreamImportSummary(Live: 4, Vod: 2, Series: 1, WarningCount: 3), summary);
        Assert.AreEqual(new SectionResult(CatalogSection.Live, SectionStatus.Ok, 4, null), sink.Sections[CatalogSection.Live]);
        Assert.AreEqual(new SectionResult(CatalogSection.Vod, SectionStatus.Ok, 2, null), sink.Sections[CatalogSection.Vod]);
        Assert.AreEqual(new SectionResult(CatalogSection.Series, SectionStatus.Ok, 1, null), sink.Sections[CatalogSection.Series]);
        Assert.AreEqual(new SectionResult(CatalogSection.Episodes, SectionStatus.NotRequested, 0, null), sink.Sections[CatalogSection.Episodes]);
    }

    [TestMethod]
    public async Task Groups_IncludeEmptyCategories_WithProviderIds()
    {
        var (sink, _) = await RunAsync(new FixtureXtreamApiClient());

        CollectionAssert.AreEqual(
            new[] { "Live:News:1", "Live:Sports:2", "Live:Empty Category:3", "Vod:Movies | Action:10", "Series:Series | Drama:20" },
            sink.Groups.Select(static g => $"{g.ContentType}:{g.RawName}:{g.ProviderCategoryId}").ToArray());
    }

    [TestMethod]
    public async Task LiveItems_MapProviderFields()
    {
        var (sink, _) = await RunAsync(new FixtureXtreamApiClient());
        var live = sink.Items.Where(static i => i.ContentType == ContentType.Live).ToList();

        Assert.AreEqual(4, live.Count);
        var cnn = live[0];
        Assert.AreEqual(ContentTypeBasis.ProviderDeclared, cnn.ContentTypeBasis);
        Assert.AreEqual("101", cnn.ProviderItemId);
        Assert.AreEqual(StreamKeys.Compute(ContentType.Live, "xtream:101"), cnn.StreamKey);
        Assert.AreEqual("cnn.us", cnn.TvgId);
        Assert.AreEqual("1", cnn.Attributes["num"]);
        Assert.AreEqual("1", cnn.Attributes["tv_archive"]);
        CollectionAssert.AreEqual(new[] { "News" }, cnn.GroupNames.ToArray());
        CollectionAssert.AreEqual(new[] { "Sports", "News" }, live[1].GroupNames.ToArray(), "category_ids fan-out is kept on one item");
        Assert.AreEqual("(untitled #2)", live[2].DisplayTitle);
        Assert.IsTrue(live[2].DisplayTitleIsFallback);
        CollectionAssert.AreEqual(new[] { "(category 99)" }, live[2].GroupNames.ToArray());
        Assert.AreEqual("http://example.invalid:8080/images/***/***/105.png", live[3].LogoUrl, "credentials in artwork paths are redacted");
    }

    [TestMethod]
    public async Task VodAndSeriesItems_KeepMetadata_AndSeriesHaveNoStreamKey()
    {
        var (sink, _) = await RunAsync(new FixtureXtreamApiClient());

        var matrix = sink.Items.Single(static i => i.ContentType == ContentType.Vod && i.ProviderItemId == "201");
        Assert.AreEqual(1999, matrix.Year);
        Assert.AreEqual("mkv", matrix.ContainerExtension);
        Assert.AreEqual("8.7", matrix.Attributes["rating"]);
        Assert.AreEqual("603", matrix.Attributes["tmdb"]);
        var series = sink.Items.Single(static i => i.ContentType == ContentType.Series);
        Assert.AreEqual("301", series.ProviderItemId);
        Assert.IsNull(series.StreamKey);
        Assert.AreEqual(2008, series.Year);
        Assert.AreEqual("1760000000", series.Attributes["last_modified"]);
        CollectionAssert.AreEqual(new[] { "Series | Drama" }, series.GroupNames.ToArray());
    }

    [TestMethod]
    public async Task Warnings_CoverInvalidItemsUnknownCategoriesAndEmptyTitles()
    {
        var (sink, _) = await RunAsync(new FixtureXtreamApiClient());

        CollectionAssert.AreEqual(
            new[] { "Live:XTREAM_EMPTY_TITLE:1", "Live:XTREAM_INVALID_ITEM:1", "Live:XTREAM_UNKNOWN_CATEGORY:1" },
            sink.Warnings.Select(static w => $"{w.Section}:{w.Code}:{w.Count}").ToArray());
    }

    [TestMethod]
    public async Task NotRequestedSections_AreRecorded_AndNotFetched()
    {
        var client = new FixtureXtreamApiClient();

        var (sink, summary) = await RunAsync(client, vod: false, series: false);

        Assert.AreEqual(SectionStatus.NotRequested, sink.Sections[CatalogSection.Vod].Status);
        Assert.AreEqual(SectionStatus.NotRequested, sink.Sections[CatalogSection.Series].Status);
        Assert.AreEqual(0, summary.Vod);
        CollectionAssert.AreEqual(new[] { "account_info", "live_categories", "live_streams" }, client.Calls);
    }

    [TestMethod]
    public async Task FailingVod_MarksOnlyVodFailed_WithRedactedError()
    {
        var client = new FixtureXtreamApiClient();
        client.Failing.Add("vod_streams");

        var (sink, _) = await RunAsync(client);

        Assert.AreEqual(SectionStatus.Ok, sink.Sections[CatalogSection.Live].Status);
        Assert.AreEqual(SectionStatus.Failed, sink.Sections[CatalogSection.Vod].Status);
        Assert.AreEqual(SectionStatus.Ok, sink.Sections[CatalogSection.Series].Status);
        StringAssert.Contains(sink.Sections[CatalogSection.Vod].ErrorSummary, "HTTP 500");
        Assert.IsFalse(sink.Items.Any(static i => i.ContentType == ContentType.Vod));
    }

    [TestMethod]
    public async Task MalformedStreamList_FailsSection()
    {
        var client = new FixtureXtreamApiClient();
        client.Overrides["live_streams"] = "<html>Service Unavailable</html>";

        var (sink, _) = await RunAsync(client);

        Assert.AreEqual(SectionStatus.Failed, sink.Sections[CatalogSection.Live].Status);
        Assert.AreEqual("The provider returned an unreadable live list.", sink.Sections[CatalogSection.Live].ErrorSummary);
    }

    [TestMethod]
    public async Task MalformedCategories_KeepItems_WithPlaceholderGroupsAndWarning()
    {
        var client = new FixtureXtreamApiClient();
        client.Overrides["live_categories"] = "oops";

        var (sink, _) = await RunAsync(client, vod: false, series: false);

        Assert.AreEqual(SectionStatus.Ok, sink.Sections[CatalogSection.Live].Status);
        CollectionAssert.AreEqual(new[] { "(category 1)" }, sink.Items[0].GroupNames.ToArray());
        Assert.IsTrue(sink.Warnings.Any(static w => w.Code == ImportWarningCodes.XtreamMalformedDocument));
    }

    [TestMethod]
    public async Task RejectedCredentials_Throw()
    {
        var client = new FixtureXtreamApiClient();
        client.Overrides["account_info"] = """{"user_info":{"auth":0}}""";

        await Assert.ThrowsExactlyAsync<XtreamAuthenticationException>(() => RunAsync(client));
    }

    [TestMethod]
    public async Task UnavailableAccountInfo_IsOnlyAWarning()
    {
        var client = new FixtureXtreamApiClient();
        client.Failing.Add("account_info");

        var (sink, _) = await RunAsync(client, vod: false, series: false);

        Assert.AreEqual(SectionStatus.Ok, sink.Sections[CatalogSection.Live].Status);
        Assert.IsTrue(sink.Warnings.Any(static w => w.Code == ImportWarningCodes.XtreamAccountInfoUnavailable));
    }

    [TestMethod]
    public async Task NoOutput_ContainsCredentials()
    {
        var (sink, _) = await RunAsync(new FixtureXtreamApiClient());

        var json = JsonSerializer.Serialize(sink.Items) + JsonSerializer.Serialize(sink.Warnings) + JsonSerializer.Serialize(sink.Sections);

        Assert.IsFalse(json.Contains("s3cret", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("alice", StringComparison.Ordinal));
    }
}
