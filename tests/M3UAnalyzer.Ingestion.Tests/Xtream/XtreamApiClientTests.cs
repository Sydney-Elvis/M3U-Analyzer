using System.Collections.Concurrent;
using System.Diagnostics;
using M3UAnalyzer.Domain.Sources;
using M3UAnalyzer.Ingestion.Fetching;
using M3UAnalyzer.Ingestion.Xtream;

namespace M3UAnalyzer.Ingestion.Tests.Xtream;

[TestClass]
public sealed class XtreamApiClientTests
{
    private static readonly XtreamCredentials Credentials = new("http://example.invalid:8080/", "al ice", "p&ss=1");

    private sealed class RecordingFetcher(string body, TimeSpan delay) : ISourceFetcher
    {
        private int _inFlight;
        public ConcurrentQueue<(string Url, long StartedTicks)> Requests { get; } = new();
        public int MaxInFlight { get; private set; }
        public List<string> TempFiles { get; } = [];

        public async Task<FetchedContent> FetchUrlAsync(string url, FetchPolicy policy, CancellationToken cancellationToken)
        {
            Requests.Enqueue((url, Stopwatch.GetTimestamp()));
            var now = Interlocked.Increment(ref _inFlight);
            lock (TempFiles) { MaxInFlight = Math.Max(MaxInFlight, now); }
            try
            {
                await Task.Delay(delay, cancellationToken);
                var path = Path.Combine(Path.GetTempPath(), $"m3ua-xt-{Guid.NewGuid():N}.json");
                await File.WriteAllTextAsync(path, body, cancellationToken);
                lock (TempFiles) { TempFiles.Add(path); }
                return new FetchedContent(path, body.Length, "application/json", false);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    [TestMethod]
    [DataRow(null, "http://example.invalid:8080/player_api.php?username=al%20ice&password=p%26ss%3D1")]
    [DataRow("get_live_streams", "http://example.invalid:8080/player_api.php?username=al%20ice&password=p%26ss%3D1&action=get_live_streams")]
    public void BuildUrl_EscapesCredentials_AndAppendsAction(string? action, string expected)
    {
        Assert.AreEqual(expected, XtreamApiClient.BuildUrl(Credentials, action));
    }

    [TestMethod]
    public async Task SeriesInfo_AddsSeriesId_AndParses()
    {
        var fetcher = new RecordingFetcher("""{"episodes":{"1":[{"id":"7","episode_num":1,"title":"A"}]}}""", TimeSpan.Zero);
        var client = new XtreamApiClient(fetcher, new XtreamRequestPolicy(2, TimeSpan.Zero), TimeProvider.System);

        var result = await client.GetSeriesInfoAsync(Credentials, 301, FetchPolicy.Default, CancellationToken.None);

        Assert.AreEqual(1, result.Items.Count);
        Assert.IsTrue(fetcher.Requests.Single().Url.EndsWith("&action=get_series_info&series_id=301", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(XtreamCatalogKind.Live, "get_live_categories")]
    [DataRow(XtreamCatalogKind.Vod, "get_vod_categories")]
    [DataRow(XtreamCatalogKind.Series, "get_series_categories")]
    public async Task Categories_UseTheRightAction(XtreamCatalogKind kind, string action)
    {
        var fetcher = new RecordingFetcher("[]", TimeSpan.Zero);
        var client = new XtreamApiClient(fetcher, new XtreamRequestPolicy(2, TimeSpan.Zero), TimeProvider.System);

        await client.GetCategoriesAsync(Credentials, kind, FetchPolicy.Default, CancellationToken.None);

        Assert.IsTrue(fetcher.Requests.Single().Url.EndsWith("&action=" + action, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TempFiles_AreDeletedAfterReading()
    {
        var fetcher = new RecordingFetcher("[]", TimeSpan.Zero);
        var client = new XtreamApiClient(fetcher, new XtreamRequestPolicy(2, TimeSpan.Zero), TimeProvider.System);

        await client.GetLiveStreamsAsync(Credentials, FetchPolicy.Default, CancellationToken.None);

        Assert.IsFalse(File.Exists(fetcher.TempFiles.Single()));
    }

    [TestMethod]
    public async Task Concurrency_IsLimited()
    {
        var fetcher = new RecordingFetcher("[]", TimeSpan.FromMilliseconds(100));
        var client = new XtreamApiClient(fetcher, new XtreamRequestPolicy(2, TimeSpan.Zero), TimeProvider.System);

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.GetLiveStreamsAsync(Credentials, FetchPolicy.Default, CancellationToken.None)));

        Assert.AreEqual(6, fetcher.Requests.Count);
        Assert.IsTrue(fetcher.MaxInFlight <= 2, $"max in flight {fetcher.MaxInFlight}");
    }

    [TestMethod]
    public async Task RequestStarts_AreSpacedByMinDelay()
    {
        var fetcher = new RecordingFetcher("[]", TimeSpan.Zero);
        var client = new XtreamApiClient(fetcher, new XtreamRequestPolicy(4, TimeSpan.FromMilliseconds(100)), TimeProvider.System);

        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => client.GetLiveStreamsAsync(Credentials, FetchPolicy.Default, CancellationToken.None)));

        var starts = fetcher.Requests.Select(static r => r.StartedTicks).OrderBy(static t => t).ToArray();
        for (var i = 1; i < starts.Length; i++)
        {
            var gap = Stopwatch.GetElapsedTime(starts[i - 1], starts[i]);
            Assert.IsTrue(gap >= TimeSpan.FromMilliseconds(90), $"gap {gap.TotalMilliseconds} ms");
        }
    }

    [TestMethod]
    public async Task FetchFailure_PropagatesUnchanged()
    {
        var client = new XtreamApiClient(new ThrowingFetcher(), XtreamRequestPolicy.Default, TimeProvider.System);

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(() => client.GetLiveStreamsAsync(Credentials, FetchPolicy.Default, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.Timeout, ex.Kind);
    }

    private sealed class ThrowingFetcher : ISourceFetcher
    {
        public Task<FetchedContent> FetchUrlAsync(string url, FetchPolicy policy, CancellationToken cancellationToken)
            => throw new FetchFailedException(FetchFailureKind.Timeout, "Timed out fetching http://example.invalid:8080/player_api.php?username=***&password=***");
    }
}
