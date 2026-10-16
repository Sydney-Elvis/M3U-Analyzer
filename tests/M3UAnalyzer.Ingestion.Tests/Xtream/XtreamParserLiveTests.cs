using M3UAnalyzer.Ingestion.Xtream;

namespace M3UAnalyzer.Ingestion.Tests.Xtream;

[TestClass]
public sealed class XtreamParserLiveTests
{
    private static string Ids(IReadOnlyList<int> ids) => string.Join(",", ids);

    [TestMethod]
    public void Categories_AcceptStringOrNumberIds_AndSkipInvalidEntries()
    {
        const string json = """[{"category_id":"1","category_name":"News"},{"category_id":2,"category_name":" Sports "},{"category_id":"0","category_name":"Zero"},{"category_id":"3"},{"category_id":"4","category_name":""},"junk",{"category_id":"x","category_name":"Bad"}]""";

        var result = XtreamResponseParser.ParseCategories(json);

        CollectionAssert.AreEqual(new[] { new XtreamCategory(1, "News"), new XtreamCategory(2, "Sports") }, result.Items.ToArray());
        Assert.AreEqual(5, result.SkippedInvalid);
        Assert.IsFalse(result.DocumentMalformed);
    }

    [TestMethod]
    public void LiveStreams_ParseAllFields()
    {
        const string json = """[{"stream_id":"101","name":"CNN HD","stream_icon":"http://example.invalid/cnn.png","epg_channel_id":"cnn.us","category_id":"1","num":1,"tv_archive":1},{"stream_id":102,"name":"BBC","epg_channel_id":"","category_ids":[1,"2",0],"tv_archive":"0"},{"stream_id":0,"name":"Zero"},{"name":"No id"},{"stream_id":"103","name":null,"stream_icon":"  ","category_id":null,"num":"x"},5]""";

        var result = XtreamResponseParser.ParseLiveStreams(json);

        Assert.AreEqual(3, result.Items.Count);
        Assert.AreEqual(3, result.SkippedInvalid);
        Assert.IsFalse(result.DocumentMalformed);

        var cnn = result.Items[0];
        Assert.AreEqual(101, cnn.StreamId, "string-typed stream_id");
        Assert.AreEqual("CNN HD", cnn.Name);
        Assert.AreEqual("http://example.invalid/cnn.png", cnn.IconUrl);
        Assert.AreEqual("cnn.us", cnn.EpgChannelId);
        Assert.AreEqual("1", Ids(cnn.CategoryIds));
        Assert.AreEqual(1, cnn.Number);
        Assert.AreEqual(true, cnn.HasArchive);

        var bbc = result.Items[1];
        Assert.IsNull(bbc.EpgChannelId, "empty epg_channel_id becomes null");
        Assert.AreEqual("1,2", Ids(bbc.CategoryIds), "category_ids array wins; 0 is dropped");
        Assert.AreEqual(false, bbc.HasArchive);
        Assert.IsNull(bbc.Number);

        var third = result.Items[2];
        Assert.AreEqual(103, third.StreamId);
        Assert.AreEqual(string.Empty, third.Name, "null name becomes empty, not skipped");
        Assert.IsNull(third.IconUrl, "whitespace icon becomes null");
        Assert.AreEqual(0, third.CategoryIds.Count);
        Assert.IsNull(third.Number);
        Assert.IsNull(third.HasArchive, "missing flag is unknown, not false");
    }

    [TestMethod]
    [DataRow("""{"user_info":{}}""")]
    [DataRow("""[{"stream_id":1,""")]
    [DataRow("<html>error</html>")]
    [DataRow("")]
    public void LiveStreams_MalformedDocument_IsFlagged(string json)
    {
        var result = XtreamResponseParser.ParseLiveStreams(json);

        Assert.IsTrue(result.DocumentMalformed);
        Assert.AreEqual(0, result.Items.Count);
        Assert.AreEqual(0, result.SkippedInvalid);
    }

    [TestMethod]
    public void LiveStreams_EmptyArray_IsValidAndEmpty()
    {
        var result = XtreamResponseParser.ParseLiveStreams("[]");

        Assert.IsFalse(result.DocumentMalformed);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public void AccountInfo_Authenticated()
    {
        var info = XtreamResponseParser.ParseAccountInfo("""{"user_info":{"auth":1,"status":"Active","exp_date":"1767225600","max_connections":"2"},"server_info":{}}""");

        Assert.AreEqual(new XtreamAccountInfo(true, "Active", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 2), info);
    }

    [TestMethod]
    public void AccountInfo_Rejected()
    {
        Assert.AreEqual(new XtreamAccountInfo(false, null, null, null), XtreamResponseParser.ParseAccountInfo("""{"user_info":{"auth":0}}"""));
    }

    [TestMethod]
    public void AccountInfo_NullAndZeroValues_AreUnknown()
    {
        Assert.AreEqual(new XtreamAccountInfo(true, null, null, null), XtreamResponseParser.ParseAccountInfo("""{"user_info":{"auth":"1","exp_date":null,"max_connections":0}}"""));
    }

    [TestMethod]
    [DataRow("""{"server_info":{}}""")]
    [DataRow("<html>")]
    [DataRow("[]")]
    public void AccountInfo_Unparseable_ReturnsNull(string json)
    {
        Assert.IsNull(XtreamResponseParser.ParseAccountInfo(json));
    }
}
