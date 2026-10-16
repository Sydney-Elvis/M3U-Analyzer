using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Ingestion.M3u;

namespace M3UAnalyzer.Ingestion.Tests.M3u;

[TestClass]
public sealed class ContentTypeInferenceTests
{
    [TestMethod]
    [DataRow(null, ContentType.Unknown, ContentTypeBasis.Defaulted)]
    [DataRow("", ContentType.Unknown, ContentTypeBasis.Defaulted)]
    [DataRow("   ", ContentType.Unknown, ContentTypeBasis.Defaulted)]
    [DataRow("http://h/live/u/p/1.ts", ContentType.Live, ContentTypeBasis.UrlInferred)]
    [DataRow("http://h/u/p/1", ContentType.Live, ContentTypeBasis.Defaulted)]
    [DataRow("http://h/u/p/1.ts", ContentType.Live, ContentTypeBasis.UrlInferred)]
    [DataRow("http://h/stream.m3u8", ContentType.Live, ContentTypeBasis.UrlInferred)]
    [DataRow("http://h/movie/u/p/1.mkv", ContentType.Vod, ContentTypeBasis.UrlInferred)]
    [DataRow("http://h/series/u/p/1.mp4", ContentType.Series, ContentTypeBasis.UrlInferred)]
    [DataRow("http://h/x?type=vod", ContentType.Vod, ContentTypeBasis.UrlInferred)]
    [DataRow("http://h/x?type=live", ContentType.Live, ContentTypeBasis.UrlInferred)]
    [DataRow("http://h/file.mp4", ContentType.Vod, ContentTypeBasis.UrlInferred)]
    [DataRow("http://h/unknown", ContentType.Live, ContentTypeBasis.Defaulted)]
    [DataRow("rtmp://h/app/stream", ContentType.Live, ContentTypeBasis.Defaulted)]
    [DataRow("not a url /live/ x", ContentType.Live, ContentTypeBasis.UrlInferred)]
    [DataRow("not a url", ContentType.Live, ContentTypeBasis.Defaulted)]
    public void FromUrl_ReturnsTypeAndBasis(string? url, ContentType expectedType, ContentTypeBasis expectedBasis)
    {
        var (type, basis) = ContentTypeInference.FromUrl(url);

        Assert.AreEqual(expectedType, type, url ?? "<null>");
        Assert.AreEqual(expectedBasis, basis, url ?? "<null>");
    }
}
