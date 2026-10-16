using M3UAnalyzer.Domain.Catalog;

namespace M3UAnalyzer.Domain.Tests.Catalog;

[TestClass]
public sealed class StreamKeysTests
{
    [TestMethod]
    [DataRow(ContentType.Live, "xtream:123", "b6e11c2648ecf89701b9e3d2edc60a03")]
    [DataRow(ContentType.Vod, "xtream:123", "869385665deaed9557198211a604c7d0")]
    [DataRow(ContentType.Live, "/hls/stream.m3u8?token=abc", "a1c3eaba64fc47a38ab91a97c61a5300")]
    public void Compute_ReturnsKnownVector(ContentType contentType, string locator, string expected)
    {
        Assert.AreEqual(expected, StreamKeys.Compute(contentType, locator));
    }

    [TestMethod]
    public void Compute_IsLowerCaseHexOf32Characters()
    {
        var key = StreamKeys.Compute(ContentType.Series, "xtream:9");
        Assert.AreEqual(32, key.Length);
        Assert.IsTrue(key.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')), key);
    }

    [TestMethod]
    public void Compute_DoesNotContainTheLocator()
    {
        var key = StreamKeys.Compute(ContentType.Live, "/hls/stream.m3u8?token=abc");
        Assert.IsFalse(key.Contains("token", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Compute_NullLocator_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => StreamKeys.Compute(ContentType.Live, null!));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Compute_EmptyLocator_Throws(string locator)
    {
        Assert.ThrowsExactly<ArgumentException>(() => StreamKeys.Compute(ContentType.Live, locator));
    }
}