using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace M3UAnalyzer.Ingestion.Tests.TestDoubles;

/// <summary>Minimal loopback HTTP server for fetcher tests (no external packages, no internet).</summary>
public sealed class TestHttpServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, Action<HttpListenerContext>> _handlers = new(StringComparer.Ordinal);
    private readonly Task _loop;
    private int _requestCount;

    public TestHttpServer()
    {
        Port = GetFreePort();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _loop = Task.Run(LoopAsync);
    }

    public int Port { get; }

    public int RequestCount => Volatile.Read(ref _requestCount);

    public string Url(string pathAndQuery) => $"http://127.0.0.1:{Port}{pathAndQuery}";

    public void Handle(string path, Action<HttpListenerContext> handler) => _handlers[path] = handler;

    public static void Write(HttpListenerContext context, int status, string body) => WriteBytes(context, status, Encoding.UTF8.GetBytes(body));

    public static void WriteBytes(HttpListenerContext context, int status, byte[] body)
    {
        context.Response.StatusCode = status;
        context.Response.ContentLength64 = body.Length;
        context.Response.OutputStream.Write(body);
        context.Response.Close();
    }

    public static void Redirect(HttpListenerContext context, string location)
    {
        context.Response.StatusCode = 302;
        context.Response.RedirectLocation = location;
        context.Response.Close();
    }

    private async Task LoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (!_listener.IsListening)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return;
            }

            Interlocked.Increment(ref _requestCount);
            _ = Task.Run(() =>
            {
                try
                {
                    if (_handlers.TryGetValue(context.Request.Url!.AbsolutePath, out var handler)) handler(context);
                    else Write(context, 404, "not found");
                }
                catch (Exception)
                {
                    // Client may have disconnected (timeouts); ignore in tests.
                }
            });
        }
    }

    private static int GetFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Close();
        try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
    }
}
