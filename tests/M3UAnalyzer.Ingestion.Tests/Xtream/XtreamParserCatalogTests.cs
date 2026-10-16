using M3UAnalyzer.Ingestion.Xtream;

namespace M3UAnalyzer.Ingestion.Tests.Xtream;

[TestClass]
public sealed class XtreamParserCatalogTests
{
    private static string Ids(IReadOnlyList<int> ids) => string.Join(",", ids);

    [TestMethod]
    public void VodStreams_ParseOptionalMetadata()
    {
        const string json = """[{"stream_id":"201","name":"The Matrix","stream_icon":"http://example.invalid/m.jpg","category_id":"10","container_extension":"mkv","year":"1999","rating":"8.7","tmdb":"603"},{"stream_id":202,"name":"Old","container_extension":"","year":1700,"rating":7.5,"tmdb_id":0},{"stream_id":203,"name":"No Meta","category_ids":["11","12"]}]""";

        var result = XtreamResponseParser.ParseVodStreams(json);

        Assert.AreEqual(3, result.Items.Count);
        Assert.AreEqual(0, result.SkippedInvalid);
        var matrix = result.Items[0];
        Assert.AreEqual(201, matrix.StreamId);
        Assert.AreEqual("10", Ids(matrix.CategoryIds));
        Assert.AreEqual("mkv", matrix.ContainerExtension);
        Assert.AreEqual(1999, matrix.Year);
        Assert.AreEqual("8.7", matrix.Rating);
        Assert.AreEqual("603", matrix.TmdbId);
        var old = result.Items[1];
        Assert.IsNull(old.ContainerExtension, "empty extension is unknown");
        Assert.IsNull(old.Year, "implausible year is dropped");
        Assert.AreEqual("7.5", old.Rating, "numeric rating kept as text");
        Assert.IsNull(old.TmdbId, "tmdb 0 means none");
        var noMeta = result.Items[2];
        Assert.AreEqual("11,12", Ids(noMeta.CategoryIds));
        Assert.IsNull(noMeta.Year);
        Assert.IsNull(noMeta.Rating);
    }

    [TestMethod]
    public void Series_ParseLastModifiedAndSkipInvalidIds()
    {
        const string json = """[{"series_id":"301","name":"Breaking Point","cover":"http://example.invalid/bp.jpg","category_id":"20","last_modified":"1760000000","year":"2008","tmdb":"1396"},{"series_id":302,"name":"No Mod","category_id":"20"},{"series_id":"bad","name":"Bad"}]""";

        var result = XtreamResponseParser.ParseSeries(json);

        Assert.AreEqual(2, result.Items.Count);
        Assert.AreEqual(1, result.SkippedInvalid);
        Assert.AreEqual(301, result.Items[0].SeriesId);
        Assert.AreEqual("http://example.invalid/bp.jpg", result.Items[0].CoverUrl);
        Assert.AreEqual(1760000000L, result.Items[0].LastModifiedEpoch);
        Assert.AreEqual(2008, result.Items[0].Year);
        Assert.AreEqual("1396", result.Items[0].TmdbId);
        Assert.AreEqual(0L, result.Items[1].LastModifiedEpoch);
        Assert.IsNull(result.Items[1].CoverUrl);
    }

    [TestMethod]
    public void SeriesInfo_EpisodesObjectKeyedBySeason()
    {
        const string json = """{"info":{"name":"Breaking Point"},"episodes":{"1":[{"id":"4001","episode_num":1,"title":"Pilot","container_extension":"mkv"},{"id":"4002","episode_num":"2","title":" Second ","container_extension":"mp4"}],"2":[{"id":"4003","episode_num":1,"title":null},{"id":"0","episode_num":2,"title":"Bad"}],"x":[{"id":"4004","title":"Odd season key","season":3}],"3":"not an array"}}""";

        var result = XtreamResponseParser.ParseSeriesInfo(json);

        CollectionAssert.AreEqual(
            new[]
            {
                new XtreamEpisode(4001, 1, 1, "Pilot", "mkv"),
                new XtreamEpisode(4002, 1, 2, "Second", "mp4"),
                new XtreamEpisode(4003, 2, 1, "", null),
                new XtreamEpisode(4004, 3, null, "Odd season key", null),
            },
            result.Items.ToArray());
        Assert.AreEqual(2, result.SkippedInvalid, "id 0 and the non-array season");
        Assert.IsFalse(result.DocumentMalformed);
    }

    [TestMethod]
    public void SeriesInfo_EpisodesArrayOfArrays()
    {
        var result = XtreamResponseParser.ParseSeriesInfo("""{"episodes":[[{"id":5001,"season":1,"episode_num":1,"title":"A"}],[{"id":5002,"season":"2","episode_num":1,"title":"B"}],"junk"]}""");

        CollectionAssert.AreEqual(
            new[] { new XtreamEpisode(5001, 1, 1, "A", null), new XtreamEpisode(5002, 2, 1, "B", null) },
            result.Items.ToArray());
        Assert.AreEqual(1, result.SkippedInvalid);
    }

    [TestMethod]
    public void SeriesInfo_EmptyEpisodesObject_IsValidAndEmpty()
    {
        var result = XtreamResponseParser.ParseSeriesInfo("""{"episodes":{}}""");

        Assert.IsFalse(result.DocumentMalformed);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    [DataRow("""{"info":{}}""")]
    [DataRow("not json")]
    [DataRow("""{"episodes":"none"}""")]
    [DataRow("[]")]
    public void SeriesInfo_Malformed_IsFlagged(string json)
    {
        var result = XtreamResponseParser.ParseSeriesInfo(json);

        Assert.IsTrue(result.DocumentMalformed);
        Assert.AreEqual(0, result.Items.Count);
    }
}
