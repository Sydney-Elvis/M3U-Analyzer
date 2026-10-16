using System.Globalization;
using System.Text;
using M3Undle.Core.Providers;
using M3UAnalyzer.Domain.Sources;
using M3UAnalyzer.Ingestion.Fetching;

namespace M3UAnalyzer.Ingestion.Xtream;

public sealed record XtreamRequestPolicy(int MaxConcurrentRequests, TimeSpan MinDelayBetweenRequests)
{
    public static XtreamRequestPolicy Default { get; } = new(2, TimeSpan.FromMilliseconds(250));
}

public enum XtreamCatalogKind { Live, Vod, Series }

public interface IXtreamApiClient
{
    Task<XtreamAccountInfo?> GetAccountInfoAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken);
    Task<XtreamParseResult<XtreamCategory>> GetCategoriesAsync(XtreamCredentials credentials, XtreamCatalogKind kind, FetchPolicy policy, CancellationToken cancellationToken);
    Task<XtreamParseResult<XtreamLiveStream>> GetLiveStreamsAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken);
    Task<XtreamParseResult<XtreamVodStream>> GetVodStreamsAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken);
    Task<XtreamParseResult<XtreamSeriesSummary>> GetSeriesAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken);
    Task<XtreamParseResult<XtreamEpisode>> GetSeriesInfoAsync(XtreamCredentials credentials, int seriesId, FetchPolicy policy, CancellationToken cancellationToken);
}

public sealed class XtreamApiClient(ISourceFetcher fetcher, XtreamRequestPolicy requestPolicy, TimeProvider timeProvider) : IXtreamApiClient
{
    private readonly SemaphoreSlim _concurrency = new(requestPolicy.MaxConcurrentRequests);
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private DateTimeOffset _lastStart = DateTimeOffset.MinValue;

    public static string BuildUrl(XtreamCredentials credentials, string? action, IReadOnlyDictionary<string, string>? extra = null)
    {
        var url = XtreamProviderUrls.BuildPlayerApiUrl(
            credentials.BaseUrl.TrimEnd('/'),
            credentials.Username,
            credentials.Password);
        if (action is not null)
        {
            url += "&action=" + Uri.EscapeDataString(action);
        }

        if (extra is not null)
        {
            var builder = new StringBuilder(url);
            foreach (var (key, value) in extra)
            {
                builder.Append('&')
                    .Append(Uri.EscapeDataString(key))
                    .Append('=')
                    .Append(Uri.EscapeDataString(value));
            }
            url = builder.ToString();
        }

        return url;
    }

    public async Task<XtreamAccountInfo?> GetAccountInfoAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken)
    {
        var json = await FetchTextAsync(BuildUrl(credentials, null), policy, cancellationToken);
        return XtreamResponseParser.ParseAccountInfo(json);
    }

    public async Task<XtreamParseResult<XtreamCategory>> GetCategoriesAsync(XtreamCredentials credentials, XtreamCatalogKind kind, FetchPolicy policy, CancellationToken cancellationToken)
    {
        var action = kind switch
        {
            XtreamCatalogKind.Live => "get_live_categories",
            XtreamCatalogKind.Vod => "get_vod_categories",
            XtreamCatalogKind.Series => "get_series_categories",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var json = await FetchTextAsync(BuildUrl(credentials, action), policy, cancellationToken);
        return XtreamResponseParser.ParseCategories(json);
    }

    public async Task<XtreamParseResult<XtreamLiveStream>> GetLiveStreamsAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken)
    {
        var json = await FetchTextAsync(BuildUrl(credentials, "get_live_streams"), policy, cancellationToken);
        return XtreamResponseParser.ParseLiveStreams(json);
    }

    public async Task<XtreamParseResult<XtreamVodStream>> GetVodStreamsAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken)
    {
        var json = await FetchTextAsync(BuildUrl(credentials, "get_vod_streams"), policy, cancellationToken);
        return XtreamResponseParser.ParseVodStreams(json);
    }

    public async Task<XtreamParseResult<XtreamSeriesSummary>> GetSeriesAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken)
    {
        var json = await FetchTextAsync(BuildUrl(credentials, "get_series"), policy, cancellationToken);
        return XtreamResponseParser.ParseSeries(json);
    }

    public async Task<XtreamParseResult<XtreamEpisode>> GetSeriesInfoAsync(XtreamCredentials credentials, int seriesId, FetchPolicy policy, CancellationToken cancellationToken)
    {
        var extra = new Dictionary<string, string>
        {
            ["series_id"] = seriesId.ToString(CultureInfo.InvariantCulture),
        };
        var json = await FetchTextAsync(BuildUrl(credentials, "get_series_info", extra), policy, cancellationToken);
        return XtreamResponseParser.ParseSeriesInfo(json);
    }

    private async Task<string> FetchTextAsync(string url, FetchPolicy policy, CancellationToken cancellationToken)
    {
        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            await _startGate.WaitAsync(cancellationToken);
            try
            {
                var now = timeProvider.GetUtcNow();
                var nextStart = _lastStart + requestPolicy.MinDelayBetweenRequests;
                if (now < nextStart)
                {
                    await Task.Delay(nextStart - now, timeProvider, cancellationToken);
                }
                _lastStart = timeProvider.GetUtcNow();
            }
            finally
            {
                _startGate.Release();
            }

            using var content = await fetcher.FetchUrlAsync(url, policy, cancellationToken);
            return await content.ReadAllTextAsync(cancellationToken);
        }
        finally
        {
            _concurrency.Release();
        }
    }
}
