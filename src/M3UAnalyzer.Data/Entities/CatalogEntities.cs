using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;
using M3UAnalyzer.Domain.Sources;

namespace M3UAnalyzer.Data.Entities;

public sealed class SourceEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SourceKind Kind { get; set; }
    public string DisplayLocation { get; set; } = string.Empty;
    public string? ProtectedSecret { get; set; }
    public bool IncludeVod { get; set; }
    public bool IncludeSeries { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string? CurrentSnapshotId { get; set; }
}

public sealed class SnapshotEntity
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public SnapshotStatus Status { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public long? DurationMs { get; set; }
    public long? BytesFetched { get; set; }
    public string? CoreReference { get; set; }
    public int RulesVersion { get; set; }
    public string? ErrorSummary { get; set; }
}

public sealed class SnapshotSectionEntity
{
    public string SnapshotId { get; set; } = string.Empty;
    public CatalogSection Section { get; set; }
    public SectionStatus Status { get; set; }
    public int ItemCount { get; set; }
    public string? ErrorSummary { get; set; }
}

public sealed class ImportWarningEntity
{
    public long Id { get; set; }
    public string SnapshotId { get; set; } = string.Empty;
    public CatalogSection Section { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Count { get; set; }
    public string SamplesJson { get; set; } = "[]";
}

public sealed class CatalogGroupEntity
{
    public long Id { get; set; }
    public string SnapshotId { get; set; } = string.Empty;
    public ContentType ContentType { get; set; }
    public string RawName { get; set; } = string.Empty;
    public string? ProviderCategoryId { get; set; }
    public int ItemCount { get; set; }
}

public sealed class ItemEntity
{
    public long Id { get; set; }
    public string SnapshotId { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public ContentType ContentType { get; set; }
    public ContentTypeBasis ContentTypeBasis { get; set; }
    public string RawTitle { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = string.Empty;
    public bool DisplayTitleIsFallback { get; set; }
    public string? ProviderItemId { get; set; }
    public string? StreamKey { get; set; }
    public string? TvgId { get; set; }
    public string? TvgName { get; set; }
    public string? LogoUrl { get; set; }
    public string? AttributesJson { get; set; }
    public string? WithheldAttributeKeysJson { get; set; }
    public int? Year { get; set; }
    public string? ContainerExtension { get; set; }
}

public sealed class ItemGroupEntity
{
    public long ItemId { get; set; }
    public long GroupId { get; set; }
}

public sealed class EpisodeEntity
{
    public long Id { get; set; }
    public string SnapshotId { get; set; } = string.Empty;
    public long SeriesItemId { get; set; }
    public int? Season { get; set; }
    public int? EpisodeNumber { get; set; }
    public string ProviderEpisodeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ContainerExtension { get; set; }
}

public sealed class SeriesEpisodeLoadEntity
{
    public long SeriesItemId { get; set; }
    public SectionStatus Status { get; set; }
    public DateTime LoadedUtc { get; set; }
    public int EpisodeCount { get; set; }
    public string? ErrorSummary { get; set; }
}
