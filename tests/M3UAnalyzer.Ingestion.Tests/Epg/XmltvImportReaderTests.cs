using M3UAnalyzer.Domain.Epg;
using M3UAnalyzer.Ingestion.Epg;

namespace M3UAnalyzer.Ingestion.Tests.Epg;

[TestClass]
public sealed class XmltvImportReaderTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "xmltv", name));

    [TestMethod]
    public void GuideSmall_RawAndParsedCounts()
    {
        var result = XmltvImportReader.Read("guide", Fixture("guide-small.xml"));

        Assert.AreEqual(new EpgParseStats(9, 8, 28, 26, false), result.Stats);
        Assert.AreEqual(1, result.Stats.DroppedChannels);
        Assert.AreEqual(2, result.Stats.DroppedProgrammes);
        Assert.AreEqual("guide", result.Catalogue.SourceId);
    }

    [TestMethod]
    public void TruncatedGuide_KeepsPartialData_AndIsFlagged()
    {
        var result = XmltvImportReader.Read("t", Fixture("guide-truncated.xml"));

        Assert.AreEqual(new EpgParseStats(3, 3, 3, 2, true), result.Stats);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void EmptyDocument_IsAllZeros_NotTruncated(string xml)
    {
        Assert.AreEqual(new EpgParseStats(0, 0, 0, 0, false), XmltvImportReader.Read("e", xml).Stats);
    }

    [TestMethod]
    public void NonXmltvDocument_HasNoChannels()
    {
        Assert.AreEqual(new EpgParseStats(0, 0, 0, 0, false), XmltvImportReader.Read("h", "<html><body>Service Unavailable</body></html>").Stats);
    }

    [TestMethod]
    public void DtdDeclarations_AreIgnored()
    {
        const string xml = "<?xml version=\"1.0\"?><!DOCTYPE tv SYSTEM \"xmltv.dtd\"><tv><channel id=\"a\"><display-name>A</display-name></channel></tv>";

        Assert.AreEqual(new EpgParseStats(1, 1, 0, 0, false), XmltvImportReader.Read("d", xml).Stats);
    }
}
