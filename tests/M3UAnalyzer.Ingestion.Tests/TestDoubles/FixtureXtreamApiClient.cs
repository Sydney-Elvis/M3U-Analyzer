using M3UAnalyzer.Domain.Sources;
using M3UAnalyzer.Ingestion.Fetching;
using M3UAnalyzer.Ingestion.Xtream;

namespace M3UAnalyzer.Ingestion.Tests.TestDoubles;

/// <summary>Serves tests/Fixtures/xtream/*.json through the real parsers. Responses can be overridden or made to fail.</summary>
public sealed class FixtureXtreamApiClient : IXtreamApiClient
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "xtream");

    public Dictionary<string, string> Overrides { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Failing { get; } = new(StringComparer.Ordinal);
    public List<string> Calls { get; } = [];

    private string Load(string name)
    {
        Calls.Add(name);
        if (Failing.Contains(name)) throw new FetchFailedException(FetchFailureKind.HttpStatus, $"HTTP 500 from http://example.invalid/player_api.php?username=***&password=*** ({name})");
        return Overrides.TryGetValue(name, out var json) ? json : File.ReadAllText(Path.Combine(Root, name + ".json"));
    }

    public Task<XtreamAccountInfo?> GetAccountInfoAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken)
        => Task.FromResult(XtreamResponseParser.ParseAccountInfo(Load("account_info")));

    public Task<XtreamParseResult<XtreamCategory>> GetCategoriesAsync(XtreamCredentials credentials, XtreamCatalogKind kind, FetchPolicy policy, CancellationToken cancellationToken)
        => Task.FromResult(XtreamResponseParser.ParseCategories(Load(kind switch { XtreamCatalogKind.Live => "live_categories", XtreamCatalogKind.Vod => "vod_categories", _ => "series_categories" })));

    public Task<XtreamParseResult<XtreamLiveStream>> GetLiveStreamsAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken)
        => Task.FromResult(XtreamResponseParser.ParseLiveStreams(Load("live_streams")));

    public Task<XtreamParseResult<XtreamVodStream>> GetVodStreamsAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken)
        => Task.FromResult(XtreamResponseParser.ParseVodStreams(Load("vod_streams")));

    public Task<XtreamParseResult<XtreamSeriesSummary>> GetSeriesAsync(XtreamCredentials credentials, FetchPolicy policy, CancellationToken cancellationToken)
        => Task.FromResult(XtreamResponseParser.ParseSeries(Load("series")));

    public Task<XtreamParseResult<XtreamEpisode>> GetSeriesInfoAsync(XtreamCredentials credentials, int seriesId, FetchPolicy policy, CancellationToken cancellationToken)
        => Task.FromResult(XtreamResponseParser.ParseSeriesInfo(Load($"series_info_{seriesId}")));
}
