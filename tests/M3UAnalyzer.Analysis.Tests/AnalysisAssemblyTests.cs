using M3UAnalyzer.Analysis;

namespace M3UAnalyzer.Analysis.Tests;

[TestClass]
public sealed class AnalysisAssemblyTests
{
    [TestMethod]
    public void AnalysisAssembly_CanUseCore()
    {
        // Proves the Core reference resolves for Analysis.
        Assert.AreEqual("cnn", M3Undle.Core.Epg.EpgChannelMatcher.NormalizeName("CNN"));
        Assert.AreEqual("M3UAnalyzer.Analysis", typeof(AnalysisAssemblyMarker).Assembly.GetName().Name);
    }
}