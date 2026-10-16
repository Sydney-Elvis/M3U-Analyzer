using System.Diagnostics;
using M3UAnalyzer.Domain.Security;

namespace M3UAnalyzer.Domain.Tests.Security;

[TestClass]
public sealed class TextRedactorTests
{
    [TestMethod]
    [DataRow("Playlist fetch failed: http://example.invalid/get.php?username=alice&password=s3cret", "Playlist fetch failed: http://example.invalid/get.php?username=***&password=***")]
    [DataRow("a http://x.invalid/live/u/p/1.ts and https://y.invalid/a?token=t done", "a http://x.invalid/live/***/***/1.ts and https://y.invalid/a?token=*** done")]
    [DataRow("\"http://alice:pw@h.invalid/x\"", "\"http://***@h.invalid/x\"")]
    [DataRow("<http://h.invalid/live/a/b/1.ts>", "<http://h.invalid/live/***/***/1.ts>")]
    [DataRow("no urls here", "no urls here")]
    [DataRow("C:\\path\\file.m3u", "C:\\path\\file.m3u")]
    [DataRow("", "")]
    public void Redact_RedactsEveryEmbeddedUrl(string input, string expected)
    {
        Assert.AreEqual(expected, TextRedactor.Redact(input));
    }

    [TestMethod]
    [DataRow("Fetch failed: http://example.invalid:8080/alice/s3cret/12345.", "Fetch failed: http://example.invalid:8080/***/***/12345.", DisplayName = "Short form + period")]
    [DataRow("Fetch failed: http://example.invalid:8080/alice/s3cret/12345.ts.", "Fetch failed: http://example.invalid:8080/***/***/12345.ts.", DisplayName = "Short form with extension + period")]
    [DataRow("http://example.invalid:8080/alice/s3cret/12345.", "http://example.invalid:8080/***/***/12345.", DisplayName = "Whole text is a URL + period")]
    [DataRow("Skipped http://example.invalid:8080/alice/s3cret/12345.ts, retrying", "Skipped http://example.invalid:8080/***/***/12345.ts, retrying", DisplayName = "Short form + comma")]
    [DataRow("Stream (http://example.invalid:8080/alice/s3cret/12345) timed out", "Stream (http://example.invalid:8080/***/***/12345) timed out", DisplayName = "Short form + closing parenthesis")]
    [DataRow("Bad items [http://example.invalid:8080/alice/s3cret/12345]", "Bad items [http://example.invalid:8080/***/***/12345]", DisplayName = "Short form + closing bracket")]
    [DataRow("{http://example.invalid:8080/alice/s3cret/12345}", "{http://example.invalid:8080/***/***/12345}", DisplayName = "Short form + closing brace")]
    [DataRow("See (http://example.invalid:8080/alice/s3cret/12345.ts).", "See (http://example.invalid:8080/***/***/12345.ts).", DisplayName = "Short form + parenthesis and period")]
    [DataRow("Timeout: http://example.invalid:8080/alice/s3cret/12345; giving up", "Timeout: http://example.invalid:8080/***/***/12345; giving up", DisplayName = "Short form + semicolon")]
    [DataRow("Error at http://example.invalid:8080/alice/s3cret/12345: refused", "Error at http://example.invalid:8080/***/***/12345: refused", DisplayName = "Short form + colon")]
    [DataRow("Dead: http://example.invalid:8080/alice/s3cret/12345!", "Dead: http://example.invalid:8080/***/***/12345!", DisplayName = "Short form + exclamation mark")]
    [DataRow("Is http://example.invalid:8080/alice/s3cret/12345?", "Is http://example.invalid:8080/***/***/12345?", DisplayName = "Short form + question mark")]
    [DataRow("Failed: http://example.invalid/live/alice/s3cret/1.ts.", "Failed: http://example.invalid/live/***/***/1.ts.", DisplayName = "Keyword form + period")]
    [DataRow("Failed: http://example.invalid/get.php?username=alice&password=s3cret.", "Failed: http://example.invalid/get.php?username=***&password=***.", DisplayName = "Query credentials + period kept")]
    [DataRow("Ended http://example.invalid/hls/stream.m3u8?token=abc123).", "Ended http://example.invalid/hls/stream.m3u8?token=***).", DisplayName = "Token + parenthesis and period kept")]
    [DataRow("Failed (http://alice:s3cret@example.invalid/x), stop", "Failed (http://***@example.invalid/x), stop", DisplayName = "User info + parenthesis and comma")]
    [DataRow("Trailing http:// only.", "Trailing http:// only.", DisplayName = "Scheme with nothing after it is unchanged")]
    public void Redact_TrailingPunctuation_StaysOutsideTheUrl(string input, string expected)
    {
        Assert.AreEqual(expected, TextRedactor.Redact(input));
    }

    [TestMethod]
    public void Redact_TrailingPunctuation_NeverLeaksTheSecrets()
    {
        var urls = new[]
        {
            "http://example.invalid:8080/alice/s3cret/12345",
            "http://example.invalid:8080/alice/s3cret/12345.ts",
            "http://example.invalid/live/alice/s3cret/1.ts",
            "http://example.invalid/get.php?username=alice&password=s3cret",
            "http://alice:s3cret@example.invalid/x",
        };
        var suffixes = new[] { ".", ",", ";", ":", "!", "?", ")", "]", "}", ").", "],", "))" };

        foreach (var url in urls)
        {
            foreach (var suffix in suffixes)
            {
                var output = TextRedactor.Redact("Failed (" + url + suffix + " next")!;
                Assert.IsFalse(output.Contains("alice", StringComparison.Ordinal), output);
                Assert.IsFalse(output.Contains("s3cret", StringComparison.Ordinal), output);
                Assert.IsTrue(output.EndsWith(suffix + " next", StringComparison.Ordinal), output);
            }
        }
    }

    [TestMethod]
    public void Redact_LongTextWithoutUrls_FinishesQuickly()
    {
        var text = new string('a', 200_000);

        var stopwatch = Stopwatch.StartNew();
        var output = TextRedactor.Redact(text);
        stopwatch.Stop();

        Assert.AreEqual(text, output);
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2000, $"took {stopwatch.ElapsedMilliseconds} ms");
    }

    [TestMethod]
    public void Redact_Null_ReturnsNull()
    {
        Assert.IsNull(TextRedactor.Redact(null));
    }
}