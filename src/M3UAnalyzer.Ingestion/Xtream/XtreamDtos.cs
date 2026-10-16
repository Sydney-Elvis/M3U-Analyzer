namespace M3UAnalyzer.Ingestion.Xtream;

public sealed record XtreamCategory(int CategoryId, string Name);

public sealed record XtreamLiveStream(
    int StreamId,
    string Name,
    string? IconUrl,
    string? EpgChannelId,
    IReadOnlyList<int> CategoryIds,
    int? Number,
    bool? HasArchive);

public sealed record XtreamAccountInfo(bool Authenticated, string? Status, DateTimeOffset? ExpiresUtc, int? MaxConnections);

public sealed record XtreamVodStream(
    int StreamId,
    string Name,
    string? IconUrl,
    IReadOnlyList<int> CategoryIds,
    string? ContainerExtension,
    int? Year,
    string? Rating,
    string? TmdbId);

public sealed record XtreamSeriesSummary(
    int SeriesId,
    string Name,
    string? CoverUrl,
    IReadOnlyList<int> CategoryIds,
    long LastModifiedEpoch,
    int? Year,
    string? TmdbId);

public sealed record XtreamEpisode(int EpisodeId, int? Season, int? EpisodeNumber, string Title, string? ContainerExtension);

/// <summary>Parsed items, the number of array elements skipped as invalid, and whether the document itself was unusable.</summary>
public sealed record XtreamParseResult<T>(IReadOnlyList<T> Items, int SkippedInvalid, bool DocumentMalformed);
