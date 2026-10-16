using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using M3UAnalyzer.Ingestion.Fetching;
using M3UAnalyzer.Ingestion.Tests.TestDoubles;

namespace M3UAnalyzer.Ingestion.Tests.Fetching;

[TestClass]
[DoNotParallelize]
public sealed class SourceFetcherTests
{
    private static readonly FetchPolicy LoopbackPolicy = new(1024 * 1024, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), 5, AllowPrivateNetworks: true);
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);
    private string _temp = null!;
    private SourceFetcher _fetcher = null!;
    private TestHttpServer _server = null!;
    private ManualResetEventSlim _release = null!;

    [TestInitialize]
    public void Initialize()
    {
        _temp = Path.Combine(Path.GetTempPath(), $"m3ua-fetch-{Guid.NewGuid():N}");
        _fetcher = new SourceFetcher(_temp, "M3UAnalyzer-Tests/1.0");
        _server = new TestHttpServer();
        _release = new ManualResetEventSlim(false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _release.Set();
        _server.Dispose();
        _fetcher.Dispose();
        _release.Dispose();
        if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
    }

    [TestMethod]
    public async Task Ok_DownloadsToTempFile_AndDisposeDeletesIt()
    {
        _server.Handle("/playlist.m3u", ctx => TestHttpServer.Write(ctx, 200, "#EXTM3U\n"));

        string path;
        using (var content = await _fetcher.FetchUrlAsync(_server.Url("/playlist.m3u"), LoopbackPolicy, CancellationToken.None))
        {
            path = content.TempFilePath;
            Assert.AreEqual("#EXTM3U\n", await content.ReadAllTextAsync(CancellationToken.None));
            Assert.AreEqual(8L, content.Bytes);
            Assert.IsFalse(content.WasGzipFile);
        }

        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public async Task LoopbackHost_IsBlocked_WhenPrivateNetworksNotAllowed()
    {
        _server.Handle("/x", ctx => TestHttpServer.Write(ctx, 200, "x"));

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync(_server.Url("/x"), LoopbackPolicy with { AllowPrivateNetworks = false }, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.BlockedAddress, ex.Kind);
        Assert.AreEqual(0, _server.RequestCount, "no request may reach a blocked host");
    }

    [TestMethod]
    public async Task LocalhostName_IsBlocked_WhenPrivateNetworksNotAllowed()
    {
        _server.Handle("/x", ctx => TestHttpServer.Write(ctx, 200, "x"));

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync($"http://localhost:{_server.Port}/x", LoopbackPolicy with { AllowPrivateNetworks = false }, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.BlockedAddress, ex.Kind);
        Assert.AreEqual(0, _server.RequestCount, "a host name that resolves to loopback is blocked at connect time");
    }

    [TestMethod]
    public async Task PrivateNetworkPermission_IsNotReusedByRestrictedFetch()
    {
        _server.Handle("/x", ctx => TestHttpServer.Write(ctx, 200, "x"));

        using (var allowed = await _fetcher.FetchUrlAsync(_server.Url("/x"), LoopbackPolicy, CancellationToken.None))
        {
            Assert.AreEqual("x", await allowed.ReadAllTextAsync(CancellationToken.None));
        }

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync(_server.Url("/x"), LoopbackPolicy with { AllowPrivateNetworks = false }, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.BlockedAddress, ex.Kind);
        Assert.AreEqual(1, _server.RequestCount, "the pooled connection from the permitted fetch must not be reused");
    }

    [TestMethod]
    public async Task EnvironmentProxy_IsNeverUsed()
    {
        _server.Handle("/direct", ctx => TestHttpServer.Write(ctx, 200, "direct"));
        using var proxy = new ConnectionCounter();
        var original = HttpClient.DefaultProxy;
        HttpClient.DefaultProxy = new FixedProxy(new Uri($"http://127.0.0.1:{proxy.Port}/"));
        try
        {
            using var fetcher = new SourceFetcher(_temp, "M3UAnalyzer-Tests/1.0");
            using var content = await fetcher.FetchUrlAsync(_server.Url("/direct"), LoopbackPolicy, CancellationToken.None);

            Assert.AreEqual("direct", await content.ReadAllTextAsync(CancellationToken.None));
            Assert.AreEqual(0, proxy.ConnectionCount, "the fetcher must connect directly, never through a configured proxy");
        }
        finally
        {
            HttpClient.DefaultProxy = original;
        }
    }

    [TestMethod]
    [DataRow("file:///etc/passwd")]
    [DataRow("ftp://alice:s3cret@example.invalid/x")]
    [DataRow("not a url alice s3cret")]
    [DataRow("/live/alice/s3cret/1.ts")]
    public async Task NonHttpUrls_AreRejected_WithoutEchoingTheUrl(string url)
    {
        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(() => _fetcher.FetchUrlAsync(url, LoopbackPolicy, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.InvalidUrl, ex.Kind);
        Assert.AreEqual("Only http and https URLs can be fetched.", ex.Message);
    }

    [TestMethod]
    public async Task Redirect_IsFollowed()
    {
        _server.Handle("/old", ctx => TestHttpServer.Redirect(ctx, "/new"));
        _server.Handle("/new", ctx => TestHttpServer.Write(ctx, 200, "moved"));

        using var content = await _fetcher.FetchUrlAsync(_server.Url("/old"), LoopbackPolicy, CancellationToken.None);

        Assert.AreEqual("moved", await content.ReadAllTextAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task RedirectLoop_StopsAfterMaxRedirects()
    {
        _server.Handle("/loop", ctx => TestHttpServer.Redirect(ctx, "/loop"));

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(() => _fetcher.FetchUrlAsync(_server.Url("/loop"), LoopbackPolicy, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.TooManyRedirects, ex.Kind);
        Assert.AreEqual(6, _server.RequestCount, "initial request + 5 redirects");
    }

    [TestMethod]
    [DataRow("ftp://example.invalid/alice/s3cret/1.ts")]
    [DataRow("file:///etc/passwd")]
    public async Task Redirect_ToUnsupportedScheme_IsRejected(string location)
    {
        _server.Handle("/r", ctx => TestHttpServer.Redirect(ctx, location));

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(() => _fetcher.FetchUrlAsync(_server.Url("/r"), LoopbackPolicy, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.InvalidUrl, ex.Kind);
        Assert.AreEqual("The source redirected to a URL that is not http or https.", ex.Message);
        Assert.AreEqual(1, _server.RequestCount);
    }

    [TestMethod]
    public async Task ErrorStatus_IsReported_WithEndpointOnly()
    {
        _server.Handle("/get.php", ctx => TestHttpServer.Write(ctx, 404, "nope"));

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync(_server.Url("/get.php?username=alice&password=s3cret"), LoopbackPolicy, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.HttpStatus, ex.Kind);
        Assert.AreEqual($"HTTP 404 from http://127.0.0.1:{_server.Port}.", ex.Message);
        Assert.IsNull(ex.InnerException);
        AssertTempIsEmpty();
    }

    [TestMethod]
    public async Task ErrorStatus_AfterRedirect_DoesNotEchoTheRedirectTarget()
    {
        _server.Handle("/start", ctx => TestHttpServer.Redirect(ctx, "/signed?hdnts=exp=1~hmac=SIGNEDVALUE&X-Amz-Signature=AMZSIGVALUE"));
        _server.Handle("/signed", ctx => TestHttpServer.Write(ctx, 403, "denied"));

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(() => _fetcher.FetchUrlAsync(_server.Url("/start"), LoopbackPolicy, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.HttpStatus, ex.Kind);
        Assert.AreEqual($"HTTP 403 from http://127.0.0.1:{_server.Port} (redirects followed: 1).", ex.Message);
    }

    [TestMethod]
    public async Task NetworkError_IsReported_WithEndpointOnly()
    {
        var closedPort = GetUnusedPort();

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync($"http://alice:s3cret@127.0.0.1:{closedPort}/get.php?username=alice&password=s3cret", LoopbackPolicy, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.Network, ex.Kind);
        Assert.AreEqual($"Network error fetching from http://127.0.0.1:{closedPort}: ConnectionError.", ex.Message);
        Assert.IsNull(ex.InnerException);
    }

    [TestMethod]
    public async Task OversizedBody_IsRejected_AndTempFileRemoved()
    {
        _server.Handle("/big", ctx => TestHttpServer.Write(ctx, 200, new string('x', 5000)));

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync(_server.Url("/big"), LoopbackPolicy with { MaxBytes = 1000 }, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.TooLarge, ex.Kind);
        AssertTempIsEmpty();
    }

    [TestMethod]
    public async Task GzipFileWithoutContentEncoding_IsDecompressed_AndLimitAppliesAfterDecompression()
    {
        var payload = new string('a', 4000);
        var gz = Gzip(payload);
        _server.Handle("/guide.xml.gz", ctx => TestHttpServer.WriteBytes(ctx, 200, gz));

        using (var content = await _fetcher.FetchUrlAsync(_server.Url("/guide.xml.gz"), LoopbackPolicy, CancellationToken.None))
        {
            Assert.IsTrue(content.WasGzipFile);
            Assert.AreEqual(payload, await content.ReadAllTextAsync(CancellationToken.None));
            Assert.AreEqual(4000L, content.Bytes);
        }

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync(_server.Url("/guide.xml.gz"), LoopbackPolicy with { MaxBytes = 1000 }, CancellationToken.None));
        Assert.AreEqual(FetchFailureKind.TooLarge, ex.Kind);
        AssertTempIsEmpty();
    }

    [TestMethod]
    public async Task ContentEncodingGzip_IsDecompressed_AndLimitAppliesAfterDecompression()
    {
        var payload = new string('a', 4000);
        var gz = Gzip(payload);
        Assert.IsTrue(gz.Length < 1000, "the compressed body alone must fit the limit");
        _server.Handle("/encoded", ctx =>
        {
            ctx.Response.AddHeader("Content-Encoding", "gzip");
            TestHttpServer.WriteBytes(ctx, 200, gz);
        });

        using (var content = await _fetcher.FetchUrlAsync(_server.Url("/encoded"), LoopbackPolicy, CancellationToken.None))
        {
            Assert.IsFalse(content.WasGzipFile, "HTTP-level decompression is not a gzip file");
            Assert.AreEqual(payload, await content.ReadAllTextAsync(CancellationToken.None));
            Assert.AreEqual(4000L, content.Bytes);
        }

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync(_server.Url("/encoded"), LoopbackPolicy with { MaxBytes = 1000 }, CancellationToken.None));
        Assert.AreEqual(FetchFailureKind.TooLarge, ex.Kind);
        AssertTempIsEmpty();
    }

    [TestMethod]
    public async Task StalledHeaders_TimeOut()
    {
        _server.Handle("/slow", ctx =>
        {
            _release.Wait(GateTimeout);
            TestHttpServer.Write(ctx, 200, "late");
        });

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync(_server.Url("/slow"), LoopbackPolicy with { IdleTimeout = TimeSpan.FromMilliseconds(500) }, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.Timeout, ex.Kind);
        Assert.AreEqual($"Timed out fetching from http://127.0.0.1:{_server.Port}.", ex.Message);
        Assert.IsNull(ex.InnerException);
        AssertTempIsEmpty();
    }

    [TestMethod]
    public async Task StalledBody_TimesOut_AndTempFileRemoved()
    {
        _server.Handle("/stall", ctx =>
        {
            WritePartialBody(ctx, totalLength: 100, sent: 10);
            _release.Wait(GateTimeout);
            ctx.Response.Abort();
        });

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync(_server.Url("/stall"), LoopbackPolicy with { IdleTimeout = TimeSpan.FromMilliseconds(500) }, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.Timeout, ex.Kind);
        AssertTempIsEmpty();
    }

    [TestMethod]
    public async Task TricklingBody_HitsHardTimeout_AndTempFileRemoved()
    {
        _server.Handle("/trickle", ctx =>
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength64 = 100;
            for (var i = 0; i < 100 && !_release.IsSet; i++)
            {
                ctx.Response.OutputStream.Write("x"u8);
                ctx.Response.OutputStream.Flush();
                _release.Wait(TimeSpan.FromMilliseconds(50));
            }

            ctx.Response.Abort();
        });

        var ex = await Assert.ThrowsExactlyAsync<FetchFailedException>(
            () => _fetcher.FetchUrlAsync(_server.Url("/trickle"), LoopbackPolicy with { HardTimeout = TimeSpan.FromMilliseconds(750) }, CancellationToken.None));

        Assert.AreEqual(FetchFailureKind.Timeout, ex.Kind, "each byte arrives well within the idle timeout; only the hard timeout can stop this fetch");
        AssertTempIsEmpty();
    }

    [TestMethod]
    public async Task CallerCancellation_BeforeHeaders_PropagatesAsOperationCanceled()
    {
        var requestSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.Handle("/slow", ctx =>
        {
            requestSeen.TrySetResult();
            _release.Wait(GateTimeout);
            TestHttpServer.Write(ctx, 200, "late");
        });
        using var cts = new CancellationTokenSource();

        var fetch = _fetcher.FetchUrlAsync(_server.Url("/slow"), LoopbackPolicy, cts.Token);
        await requestSeen.Task.WaitAsync(GateTimeout);
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => fetch);
        AssertTempIsEmpty();
    }

    [TestMethod]
    public async Task CallerCancellation_DuringBody_Propagates_AndTempFileRemoved()
    {
        _server.Handle("/body", ctx =>
        {
            WritePartialBody(ctx, totalLength: 100, sent: 10);
            _release.Wait(GateTimeout);
            ctx.Response.Abort();
        });
        using var cts = new CancellationTokenSource();

        var fetch = _fetcher.FetchUrlAsync(_server.Url("/body"), LoopbackPolicy, cts.Token);
        await WaitUntilAsync(() => Directory.GetFiles(_temp).Length == 1);
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => fetch);
        AssertTempIsEmpty();
    }

    private void AssertTempIsEmpty() => Assert.AreEqual(0, Directory.GetFiles(_temp).Length, "temporary files must be deleted");

    private static byte[] Gzip(string text)
    {
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(text));
        }

        return ms.ToArray();
    }

    private static void WritePartialBody(HttpListenerContext ctx, int totalLength, int sent)
    {
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentLength64 = totalLength;
        ctx.Response.OutputStream.Write(new byte[sent]);
        ctx.Response.OutputStream.Flush();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + GateTimeout;
        while (!condition())
        {
            Assert.IsTrue(DateTime.UtcNow < deadline, "condition not reached in time");
            await Task.Delay(10);
        }
    }

    private static int GetUnusedPort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    /// <summary>Stands in for a proxy: accepts and counts TCP connections, never answers.</summary>
    private sealed class ConnectionCounter : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly List<TcpClient> _clients = [];
        private int _count;

        public ConnectionCounter()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = AcceptLoopAsync();
        }

        public int Port { get; }

        public int ConnectionCount => Volatile.Read(ref _count);

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    Interlocked.Increment(ref _count);
                    lock (_clients) _clients.Add(client);
                }
            }
            catch (Exception)
            {
                // Listener stopped.
            }
        }

        public void Dispose()
        {
            _listener.Stop();
            lock (_clients) _clients.ForEach(static c => c.Dispose());
        }
    }

    private sealed class FixedProxy(Uri proxy) : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public Uri GetProxy(Uri destination) => proxy;

        public bool IsBypassed(Uri host) => false;
    }
}
