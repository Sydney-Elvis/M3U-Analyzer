using M3UAnalyzer.Domain.Epg;
using M3UAnalyzer.Ingestion.Epg;

namespace M3UAnalyzer.Ingestion.Tests.Epg;

[TestClass]
public sealed class EpgSummaryBuilderTests
{
    private static DateTimeOffset U(int day, int hour) => new(2026, 10, day, hour, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void GuideSmall_SummariesMatchFixtureReadme()
    {
        var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "xmltv", "guide-small.xml"));
        var catalogue = XmltvImportReader.Read("g", xml).Catalogue;

        var summaries = EpgSummaryBuilder.Build(catalogue);

        CollectionAssert.AreEqual(
            new[]
            {
                new EpgChannelSummary("bbcnews.uk", "BBC News", true, 5, U(15, 10), U(15, 22)),
                new EpgChannelSummary("cnn.us", "CNN", true, 12, U(15, 6), U(16, 6)),
                new EpgChannelSummary("espn.us", "ESPN", true, 3, U(15, 10), U(15, 15)),
                new EpgChannelSummary("ghost.channel", "ghost.channel", false, 1, U(15, 10), U(15, 11)),
                new EpgChannelSummary("hbo.east", "HBO East", true, 0, null, null),
                new EpgChannelSummary("nomatch.channel", "Unmatched Channel", true, 1, U(15, 10), U(15, 12)),
                new EpgChannelSummary("pbs.us", "PBS", true, 2, U(14, 8), U(14, 12)),
                new EpgChannelSummary("sky.sports", "Sky Sports Main Event", true, 1, U(15, 11), U(15, 12)),
                new EpgChannelSummary("tsn1.ca", "TSN 1", true, 1, U(15, 10), U(15, 12)),
            },
            summaries.ToArray());
    }

    [TestMethod]
    public void EmptyCatalogue_GivesNoSummaries()
    {
        Assert.AreEqual(0, EpgSummaryBuilder.Build(XmltvImportReader.Read("e", "").Catalogue).Count);
    }
}
