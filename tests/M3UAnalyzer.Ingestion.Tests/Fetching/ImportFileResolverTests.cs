using M3UAnalyzer.Ingestion.Fetching;

namespace M3UAnalyzer.Ingestion.Tests.Fetching;

[TestClass]
public sealed class ImportFileResolverTests
{
    private string _root = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), $"m3ua-imports-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "playlist.m3u"), "#EXTM3U\n");
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "sub", "nested.m3u"), "#EXTM3U\n");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void PlainFileName_ResolvesToFullPath()
    {
        Assert.AreEqual(Path.Combine(Path.GetFullPath(_root), "playlist.m3u"), ImportFileResolver.Resolve(_root, "playlist.m3u"));
    }

    [TestMethod]
    public void ImportDirectoryWithTrailingSeparator_ResolvesToFullPath()
    {
        Assert.AreEqual(Path.Combine(Path.GetFullPath(_root), "playlist.m3u"), ImportFileResolver.Resolve(_root + "/", "playlist.m3u"));
    }

    [TestMethod]
    [DataRow("../playlist.m3u")]
    [DataRow("..")]
    [DataRow(".")]
    [DataRow("sub/nested.m3u")]
    [DataRow("sub\\nested.m3u")]
    [DataRow("/etc/passwd")]
    [DataRow(" playlist.m3u")]
    [DataRow("playlist.m3u ")]
    [DataRow("")]
    [DataRow("   ")]
    public void NonPlainNames_AreRejected(string fileName)
    {
        var ex = Assert.ThrowsExactly<FetchFailedException>(() => ImportFileResolver.Resolve(_root, fileName));
        Assert.AreEqual(FetchFailureKind.FileNotAllowed, ex.Kind);
    }

    [TestMethod]
    public void MissingFile_IsReportedAsNotFound()
    {
        var ex = Assert.ThrowsExactly<FetchFailedException>(() => ImportFileResolver.Resolve(_root, "missing.m3u"));
        Assert.AreEqual(FetchFailureKind.FileNotFound, ex.Kind);
        StringAssert.Contains(ex.Message, "missing.m3u");
        Assert.IsFalse(ex.Message.Contains(_root, StringComparison.Ordinal), "message must not reveal the server path");
    }

    [TestMethod]
    public void SymbolicLink_IsRejected()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"m3ua-outside-{Guid.NewGuid():N}.m3u");
        File.WriteAllText(outside, "#EXTM3U\n");
        try
        {
            File.CreateSymbolicLink(Path.Combine(_root, "link.m3u"), outside);

            var ex = Assert.ThrowsExactly<FetchFailedException>(() => ImportFileResolver.Resolve(_root, "link.m3u"));
            Assert.AreEqual(FetchFailureKind.FileNotAllowed, ex.Kind);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [TestMethod]
    public void BlankImportDirectory_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ImportFileResolver.Resolve(" ", "playlist.m3u"));
    }
}
