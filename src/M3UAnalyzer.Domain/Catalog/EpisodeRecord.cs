namespace M3UAnalyzer.Domain.Catalog;

/// <summary>One series episode as returned by the provider. Never holds a playable URL.</summary>
public sealed record EpisodeRecord(string SeriesProviderItemId, int? Season, int? EpisodeNumber, string ProviderEpisodeId, string Title, string? ContainerExtension);
