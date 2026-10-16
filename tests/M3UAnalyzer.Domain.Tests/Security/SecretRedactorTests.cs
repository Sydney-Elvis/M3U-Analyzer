using M3UAnalyzer.Domain.Security;

namespace M3UAnalyzer.Domain.Tests.Security;

[TestClass]
public sealed class SecretRedactorTests
{
    [TestMethod]
    [DataRow("http://example.invalid:8080/live/alice/s3cret/123.ts", "http://example.invalid:8080/live/***/***/123.ts", DisplayName = "Xtream live path")]
    [DataRow("http://example.invalid/movie/alice/s3cret/456.mkv", "http://example.invalid/movie/***/***/456.mkv", DisplayName = "Xtream movie path")]
    [DataRow("http://example.invalid/series/alice/s3cret/789.mp4", "http://example.invalid/series/***/***/789.mp4", DisplayName = "Xtream series path")]
    [DataRow("http://example.invalid/panel/live/alice/s3cret/123.ts", "http://example.invalid/panel/live/***/***/123.ts", DisplayName = "Panel under sub-path")]
    [DataRow("http://example.invalid/timeshift/alice/s3cret/60/2026-10-15:10-00/123.ts", "http://example.invalid/timeshift/***/***/60/2026-10-15:10-00/123.ts", DisplayName = "Xtream timeshift path")]
    [DataRow("HTTP://EXAMPLE.INVALID/LIVE/Alice/S3cret/1.ts", "HTTP://EXAMPLE.INVALID/LIVE/***/***/1.ts", DisplayName = "Keyword match is case-insensitive and case is preserved")]
    [DataRow("http://example.invalid:8080/alice/s3cret/12345", "http://example.invalid:8080/***/***/12345", DisplayName = "Xtream short form without extension")]
    [DataRow("http://example.invalid:8080/alice/s3cret/12345.ts?token=x", "http://example.invalid:8080/***/***/12345.ts?token=***", DisplayName = "Short form plus token")]
    [DataRow("http://example.invalid/hls/channel/123.m3u8", "http://example.invalid/***/***/123.m3u8", DisplayName = "Intentional over-redaction of numeric last segment")]
    [DataRow("http://example.invalid/get.php?username=alice&password=s3cret&type=m3u_plus&output=ts", "http://example.invalid/get.php?username=***&password=***&type=m3u_plus&output=ts", DisplayName = "get.php query credentials")]
    [DataRow("http://example.invalid/player_api.php?username=alice&password=s3cret&action=get_live_streams", "http://example.invalid/player_api.php?username=***&password=***&action=get_live_streams", DisplayName = "player_api query credentials")]
    [DataRow("http://example.invalid/xmltv.php?username=alice&password=s3cret", "http://example.invalid/xmltv.php?username=***&password=***", DisplayName = "xmltv.php query credentials")]
    [DataRow("https://cdn.example.invalid/hls/stream.m3u8?token=abc123&quality=hd", "https://cdn.example.invalid/hls/stream.m3u8?token=***&quality=hd", DisplayName = "Signed token")]
    [DataRow("http://example.invalid/x.php?USERNAME=alice", "http://example.invalid/x.php?USERNAME=***", DisplayName = "Query key match is case-insensitive")]
    [DataRow("http://example.invalid/x.php?password", "http://example.invalid/x.php?password", DisplayName = "Query key without value is unchanged")]
    [DataRow("http://example.invalid?password=x", "http://example.invalid?password=***", DisplayName = "Query without path")]
    [DataRow("http://alice:s3cret@example.invalid/playlist.m3u", "http://***@example.invalid/playlist.m3u", DisplayName = "User info")]
    [DataRow("ftp://alice:pw@example.invalid/x", "ftp://***@example.invalid/x", DisplayName = "Any scheme user info")]
    [DataRow("http://example.invalid/playlist.m3u#frag", "http://example.invalid/playlist.m3u", DisplayName = "Fragment removed")]
    [DataRow("http://example.invalid/logos/cnn.png", "http://example.invalid/logos/cnn.png", DisplayName = "Ordinary URL unchanged")]
    [DataRow("http://example.invalid/live/alice", "http://example.invalid/live/alice", DisplayName = "Too few segments after keyword")]
    [DataRow("http://example.invalid", "http://example.invalid", DisplayName = "Host only")]
    [DataRow("C:\\iptv\\list.m3u", "C:\\iptv\\list.m3u", DisplayName = "Windows path unchanged")]
    [DataRow("list.m3u", "list.m3u", DisplayName = "Relative file name unchanged")]
    [DataRow("", "", DisplayName = "Empty string")]
    public void Redact_ReturnsExpectedValue(string input, string expected)
    {
        Assert.AreEqual(expected, SecretRedactor.Redact(input));
    }

    [TestMethod]
    public void Redact_Null_ReturnsNull()
    {
        Assert.IsNull(SecretRedactor.Redact(null));
    }

    [TestMethod]
    public void Redact_Output_NeverContainsTheSecrets()
    {
        var inputs = new[]
        {
            "http://example.invalid:8080/live/alice/s3cret/123.ts",
            "http://example.invalid/get.php?username=alice&password=s3cret",
            "http://alice:s3cret@example.invalid/playlist.m3u",
            "http://example.invalid:8080/alice/s3cret/12345",
        };

        foreach (var input in inputs)
        {
            var output = SecretRedactor.Redact(input)!;
            Assert.IsFalse(output.Contains("alice", StringComparison.Ordinal), output);
            Assert.IsFalse(output.Contains("s3cret", StringComparison.Ordinal), output);
        }
    }

    [TestMethod]
    public void Mask_IsThreeAsterisks()
    {
        var mask = typeof(SecretRedactor).GetField("Mask")!.GetRawConstantValue();
        Assert.AreEqual("***", mask);
    }
}
