using M3UAnalyzer.Ingestion.M3u;

namespace M3UAnalyzer.Ingestion.Tests.M3u;

[TestClass]
public sealed class M3uAttributeParserTests
{
    private static string Describe(IReadOnlyDictionary<string, string> attributes) =>
        string.Join("; ", attributes.Select(static kv => $"{kv.Key}={kv.Value}"));

    [TestMethod]
    public void StandardAttributes_AreExtractedInOrder()
    {
        var result = M3uAttributeParser.Parse(["#EXTINF:-1 tvg-id=\"cnn.us\" tvg-name=\"CNN\" tvg-logo=\"http://l/cnn.png\" group-title=\"News\",CNN HD"]);

        Assert.AreEqual("tvg-id=cnn.us; tvg-name=CNN; tvg-logo=http://l/cnn.png; group-title=News", Describe(result));
    }

    [TestMethod]
    public void Keys_AreLowerCased_AndFirstOccurrenceWins()
    {
        var result = M3uAttributeParser.Parse(["#EXTINF:-1 TVG-ID=\"A\" tvg-id=\"B\",Dup"]);

        Assert.AreEqual("tvg-id=A", Describe(result));
    }

    [TestMethod]
    public void ExtraAttributes_IncludingUnderscoreKeys_AreKept()
    {
        var result = M3uAttributeParser.Parse(["#EXTINF:-1 tvg-country=\"US\" tvg-language=\"English\" tvg-chno=\"101\" catchup=\"default\" catchup-days=\"7\" xui_id=\"9\",X"]);

        Assert.AreEqual("tvg-country=US; tvg-language=English; tvg-chno=101; catchup=default; catchup-days=7; xui_id=9", Describe(result));
    }

    [TestMethod]
    public void EmptyValue_IsKeptAsEmptyString()
    {
        var result = M3uAttributeParser.Parse(["#EXTINF:-1 tvg-id=\"\",Empty"]);

        Assert.AreEqual(string.Empty, result["tvg-id"]);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void NoAttributes_ReturnsEmpty()
    {
        Assert.AreEqual(0, M3uAttributeParser.Parse(["#EXTINF:-1,No attrs"]).Count);
        Assert.AreEqual(0, M3uAttributeParser.Parse([]).Count);
    }

    [TestMethod]
    public void ExtGrp_IsStoredUnderExtgrpKey_AndOtherDirectivesIgnored()
    {
        var result = M3uAttributeParser.Parse(["#EXTINF:-1,Title", "#EXTGRP:Sports", "#EXTVLCOPT:http-user-agent=x"]);

        Assert.AreEqual("extgrp=Sports", Describe(result));
    }

    [TestMethod]
    public void ExtGrp_IsTrimmed_AndDoesNotReplaceGroupTitle()
    {
        var result = M3uAttributeParser.Parse(["#EXTINF:-1 group-title=\"News\",Title", "#EXTGRP:  Sports  "]);

        Assert.AreEqual("group-title=News; extgrp=Sports", Describe(result));
    }

    [TestMethod]
    public void BlankExtGrp_IsIgnored()
    {
        Assert.AreEqual(0, M3uAttributeParser.Parse(["#EXTINF:-1,Title", "#EXTGRP:   "]).Count);
    }

    [TestMethod]
    public void ExtGrpKey_ConstantValue()
    {
        var value = typeof(M3uAttributeParser).GetField("ExtGrpKey")!.GetRawConstantValue();
        Assert.AreEqual("extgrp", value);
    }

    [TestMethod]
    public void NullLines_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => M3uAttributeParser.Parse(null!));
    }
}
