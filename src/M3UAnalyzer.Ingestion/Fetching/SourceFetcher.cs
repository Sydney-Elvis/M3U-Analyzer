using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace M3UAnalyzer.Ingestion.Fetching;

public sealed class SourceFetcher : ISourceFetcher, IDisposable
{
    private const int BufferSize = 80 * 1024;

    private readonly string _tempDirectory;
    private readonly HttpClient _restrictedClient;
    private readonly HttpClient _privateClient;

    public SourceFetcher(string tempDirectory, string userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);

        _tempDirectory = Path.GetFullPath(tempDirectory);
        Directory.CreateDirectory(_tempDirectory);

        _restrictedClient = CreateClient(allowPrivateNetworks: false, userAgent);
        try
        {
            _privateClient = CreateClient(allowPrivateNetworks: true, userAgent);
        }
        catch
        {
            _restrictedClient.Dispose();
            throw;
        }
    }

    public async Task<FetchedContent> FetchUrlAsync(string url, FetchPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(policy);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var originalUri) || !IsHttpUri(originalUri))
        {
            throw new FetchFailedException(FetchFailureKind.InvalidUrl, "Only http and https URLs can be fetched.");
        }

        var endpoint = GetEndpoint(originalUri);
        var client = policy.AllowPrivateNetworks ? _privateClient : _restrictedClient;
        using var hardCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        hardCts.CancelAfter(policy.HardTimeout);

        string? downloadedPath = null;
        string? decompressedPath = null;
        var succeeded = false;
        var redirectsFollowed = 0;
        var currentUri = originalUri;

        try
        {
            while (true)
            {
                hardCts.Token.ThrowIfCancellationRequested();

                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                HttpResponseMessage response;
                using (var requestCts = CancellationTokenSource.CreateLinkedTokenSource(hardCts.Token))
                {
                    requestCts.CancelAfter(policy.IdleTimeout);
                    response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestCts.Token);
                }

                using (response)
                {
                    if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is not null)
                    {
                        if (redirectsFollowed >= policy.MaxRedirects)
                        {
                            throw new FetchFailedException(FetchFailureKind.TooManyRedirects, $"Too many redirects (more than {policy.MaxRedirects}).");
                        }

                        Uri nextUri;
                        try
                        {
                            nextUri = response.Headers.Location.IsAbsoluteUri
                                ? response.Headers.Location
                                : new Uri(currentUri, response.Headers.Location);
                        }
                        catch (UriFormatException)
                        {
                            throw new FetchFailedException(FetchFailureKind.InvalidUrl, "The source redirected to a URL that is not http or https.");
                        }

                        if (!IsHttpUri(nextUri))
                        {
                            throw new FetchFailedException(FetchFailureKind.InvalidUrl, "The source redirected to a URL that is not http or https.");
                        }

                        currentUri = nextUri;
                        redirectsFollowed++;
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new FetchFailedException(
                            FetchFailureKind.HttpStatus,
                            $"HTTP {(int)response.StatusCode} from {endpoint}{RedirectSuffix(redirectsFollowed)}.");
                    }

                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    downloadedPath = NewTempPath();
                    await DownloadResponseAsync(response, downloadedPath, policy, hardCts.Token);

                    var gzipFile = await HasGzipMagicAsync(downloadedPath, hardCts.Token);
                    var finalPath = downloadedPath;
                    var bytes = new FileInfo(downloadedPath).Length;

                    if (gzipFile)
                    {
                        decompressedPath = NewTempPath();
                        bytes = await DecompressGzipFileAsync(downloadedPath, decompressedPath, policy.MaxBytes, hardCts.Token);
                        File.Delete(downloadedPath);
                        downloadedPath = null;
                        finalPath = decompressedPath;
                        decompressedPath = null;
                    }

                    succeeded = true;
                    return new FetchedContent(finalPath, bytes, contentType, gzipFile);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FetchFailedException(
                FetchFailureKind.Timeout,
                $"Timed out fetching from {endpoint}{RedirectSuffix(redirectsFollowed)}.");
        }
        catch (HttpRequestException ex)
        {
            if (ex.InnerException is FetchFailedException fetchFailure)
            {
                throw fetchFailure;
            }

            throw new FetchFailedException(
                FetchFailureKind.Network,
                $"Network error fetching from {endpoint}{RedirectSuffix(redirectsFollowed)}: {ex.HttpRequestError}.");
        }
        catch (HttpIOException ex)
        {
            throw new FetchFailedException(
                FetchFailureKind.Network,
                $"Network error fetching from {endpoint}{RedirectSuffix(redirectsFollowed)}: {ex.HttpRequestError}.");
        }
        finally
        {
            if (!succeeded)
            {
                DeleteTempFile(downloadedPath);
                DeleteTempFile(decompressedPath);
            }
        }
    }

    private static HttpClient CreateClient(bool allowPrivateNetworks, string userAgent)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            UseProxy = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = (context, ct) => ConnectAsync(context, allowPrivateNetworks, ct),
        };
        var client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        return client;
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        bool allowPrivateNetworks,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var allowed = addresses.Where(a => allowPrivateNetworks || NetworkAddressPolicy.IsPublicAddress(a)).ToArray();
        if (allowed.Length == 0)
        {
            throw new FetchFailedException(
                FetchFailureKind.BlockedAddress,
                "The source host resolves to a private or local network address, which is not allowed. Enable private source hosts in settings if this is intended.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private async Task DownloadResponseAsync(
        HttpResponseMessage response,
        string path,
        FetchPolicy policy,
        CancellationToken hardToken)
    {
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous);
        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(hardToken);
        streamCts.CancelAfter(policy.IdleTimeout);
        await using var input = await response.Content.ReadAsStreamAsync(streamCts.Token);

        var buffer = new byte[BufferSize];
        long totalBytes = 0;
        while (true)
        {
            hardToken.ThrowIfCancellationRequested();
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(hardToken);
            readCts.CancelAfter(policy.IdleTimeout);
            var count = await input.ReadAsync(buffer.AsMemory(), readCts.Token);
            if (count == 0)
            {
                break;
            }

            totalBytes += count;
            if (totalBytes > policy.MaxBytes)
            {
                throw new FetchFailedException(FetchFailureKind.TooLarge, $"Response exceeded {policy.MaxBytes} bytes.");
            }

            await output.WriteAsync(buffer.AsMemory(0, count), hardToken);
        }
    }

    private static async Task<bool> HasGzipMagicAsync(string path, CancellationToken cancellationToken)
    {
        var header = new byte[2];
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 2, FileOptions.Asynchronous);
        var read = await input.ReadAsync(header.AsMemory(), cancellationToken);
        return read == 2 && header[0] == 0x1F && header[1] == 0x8B;
    }

    private async Task<long> DecompressGzipFileAsync(
        string compressedPath,
        string decompressedPath,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(compressedPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous);
        await using var gzip = new GZipStream(input, CompressionMode.Decompress);
        await using var output = new FileStream(decompressedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous);

        var buffer = new byte[BufferSize];
        long totalBytes = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = await gzip.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
            {
                break;
            }

            totalBytes += count;
            if (totalBytes > maxBytes)
            {
                throw new FetchFailedException(FetchFailureKind.TooLarge, $"Decompressed content exceeded {maxBytes} bytes.");
            }

            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }

        return totalBytes;
    }

    private string NewTempPath() => Path.Combine(_tempDirectory, $"fetch-{Guid.NewGuid():N}.tmp");

    private static bool IsHttpUri(Uri uri) =>
        uri.IsAbsoluteUri
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string GetEndpoint(Uri uri)
    {
        var endpointUri = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
        }.Uri;
        return endpointUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
    }

    private static string RedirectSuffix(int redirectsFollowed) =>
        redirectsFollowed == 0 ? string.Empty : $" (redirects followed: {redirectsFollowed})";

    private static void DeleteTempFile(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        _restrictedClient.Dispose();
        _privateClient.Dispose();
    }
}
