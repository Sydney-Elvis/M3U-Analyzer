using M3UAnalyzer.Domain.Imports;

namespace M3UAnalyzer.Domain.Tests.Imports;

[TestClass]
public sealed class ImportWarningCollectorTests
{
    [TestMethod]
    public void NoWarnings_ReturnsEmptyList()
    {
        var collector = new ImportWarningCollector();
        Assert.AreEqual(0, collector.ToWarnings().Count);
        Assert.AreEqual(0, collector.TotalCount);
    }

    [TestMethod]
    public void SameSectionAndCode_AreAggregated()
    {
        var collector = new ImportWarningCollector();
        collector.Add(CatalogSection.Live, ImportWarningCodes.M3uMissingUrl, "A");
        collector.Add(CatalogSection.Live, ImportWarningCodes.M3uMissingUrl, "B");
        collector.Add(CatalogSection.Live, ImportWarningCodes.M3uMissingUrl);

        var warnings = collector.ToWarnings();

        Assert.AreEqual(1, warnings.Count);
        Assert.AreEqual(3, warnings[0].Count);
        CollectionAssert.AreEqual(new[] { "A", "B" }, warnings[0].Samples.ToArray());
        Assert.AreEqual(3, collector.TotalCount);
    }

    [TestMethod]
    public void Samples_AreLimitedToThreeAndDistinct()
    {
        var collector = new ImportWarningCollector();
        foreach (var sample in new[] { "a", "a", "b", "c", "d", "e" })
        {
            collector.Add(CatalogSection.Vod, ImportWarningCodes.XtreamInvalidItem, sample);
        }

        var warning = collector.ToWarnings().Single();

        Assert.AreEqual(6, warning.Count);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, warning.Samples.ToArray());
    }

    [TestMethod]
    public void Samples_AreRedacted()
    {
        var collector = new ImportWarningCollector();
        collector.Add(CatalogSection.Live, ImportWarningCodes.M3uNoGroup, "http://example.invalid/live/alice/s3cret/1.ts");

        var sample = collector.ToWarnings().Single().Samples.Single();

        Assert.AreEqual("http://example.invalid/live/***/***/1.ts", sample);
    }

    [TestMethod]
    public void Samples_WithTrailingPunctuation_AreRedacted()
    {
        var collector = new ImportWarningCollector();
        collector.Add(CatalogSection.Live, ImportWarningCodes.M3uMissingUrl, "Bad entry (http://example.invalid:8080/alice/s3cret/12345).");

        var sample = collector.ToWarnings().Single().Samples.Single();

        Assert.AreEqual("Bad entry (http://example.invalid:8080/***/***/12345).", sample);
    }

    [TestMethod]
    public void Samples_AreTrimmedAndTruncatedTo120Characters()
    {
        var collector = new ImportWarningCollector();
        collector.Add(CatalogSection.Live, ImportWarningCodes.M3uEmptyTitle, "  " + new string('x', 200) + "  ");

        var sample = collector.ToWarnings().Single().Samples.Single();

        Assert.AreEqual(120, sample.Length);
        Assert.AreEqual(new string('x', 119) + "…", sample);
    }

    [TestMethod]
    public void WhitespaceSample_IsIgnoredButCounted()
    {
        var collector = new ImportWarningCollector();
        collector.Add(CatalogSection.Live, ImportWarningCodes.M3uEmptyTitle, "   ");

        var warning = collector.ToWarnings().Single();

        Assert.AreEqual(1, warning.Count);
        Assert.AreEqual(0, warning.Samples.Count);
    }

    [TestMethod]
    public void Warnings_AreOrderedBySectionThenCode()
    {
        var collector = new ImportWarningCollector();
        collector.Add(CatalogSection.Vod, "B_CODE");
        collector.Add(CatalogSection.Live, "Z_CODE");
        collector.Add(CatalogSection.Vod, "A_CODE");
        collector.Add(CatalogSection.Live, "A_CODE");

        var keys = collector.ToWarnings().Select(static w => $"{w.Section}:{w.Code}").ToArray();

        CollectionAssert.AreEqual(new[] { "Live:A_CODE", "Live:Z_CODE", "Vod:A_CODE", "Vod:B_CODE" }, keys);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    public void EmptyCode_Throws(string code)
    {
        var collector = new ImportWarningCollector();
        Assert.ThrowsExactly<ArgumentException>(() => collector.Add(CatalogSection.Live, code));
    }
}