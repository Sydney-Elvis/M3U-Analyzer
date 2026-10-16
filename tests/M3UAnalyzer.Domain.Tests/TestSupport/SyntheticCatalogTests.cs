using M3UAnalyzer.TestSupport;

namespace M3UAnalyzer.Domain.Tests.TestSupport;

[TestClass]
public sealed class SyntheticCatalogTests
{
    private static readonly SyntheticCatalogSpec Small = new(Live: 1000, Vod: 200, SeriesEpisodes: 300);

    [TestMethod]
    public void Expectations_ForSmallSpec()
    {
        Assert.AreEqual(new SyntheticCatalogExpectations(Entries: 1500, Live: 990, Vod: 200, Series: 300, Unknown: 10, ExactDuplicates: 50), SyntheticCatalog.Expect(Small));
    }

    [TestMethod]
    public void Output_IsDeterministic()
    {
        Assert.AreEqual(SyntheticCatalog.GenerateM3u(Small), SyntheticCatalog.GenerateM3u(Small));
        Assert.AreNotEqual(SyntheticCatalog.GenerateM3u(Small), SyntheticCatalog.GenerateM3u(Small with { Seed = 7 }));
    }

    [TestMethod]
    public void Output_HasExpectedLineCounts()
    {
        var lines = SyntheticCatalog.GenerateM3u(Small).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var expected = SyntheticCatalog.Expect(Small);

        Assert.AreEqual("#EXTM3U", lines[0]);
        Assert.AreEqual(expected.Entries, lines.Count(static l => l.StartsWith("#EXTINF", StringComparison.Ordinal)));
        Assert.AreEqual(expected.Entries - expected.Unknown, lines.Count(static l => l.StartsWith("http", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Output_UsesOnlyFakeHostsAndCredentials()
    {
        var text = SyntheticCatalog.GenerateM3u(Small);

        foreach (var line in text.Split('\n').Where(static l => l.StartsWith("http", StringComparison.Ordinal)))
        {
            StringAssert.StartsWith(line, "http://gen.example.invalid:8080/");
        }

        StringAssert.Contains(text, SyntheticCatalog.Password);
    }

    [TestMethod]
    public void LargeSpec_IsGeneratedQuickly()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var text = SyntheticCatalog.GenerateM3u(new SyntheticCatalogSpec(50_000, 20_000, 30_000));

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
        Assert.IsTrue(text.Length > 1_000_000);
    }

    [TestMethod]
    public void NegativeSizes_Throw()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SyntheticCatalog.GenerateM3u(new SyntheticCatalogSpec(-1, 0, 0)));
    }
}
