namespace M3UAnalyzer.Domain.Catalog;

/// <summary>
/// One catalog entry as imported from a source. Holds source facts only; derived attributes live elsewhere.
/// Never holds a playable stream URL or credentials.
/// </summary>
public sealed record RawCatalogItem
{
    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Zero-based position of the entry in the provider output.</summary>
    public required int Ordinal { get; init; }

    public required ContentType ContentType { get; init; }

    public required ContentTypeBasis ContentTypeBasis { get; init; }

    /// <summary>Title exactly as provided; may be empty.</summary>
    public required string RawTitle { get; init; }

    /// <summary>Title to display. Equals <see cref="RawTitle"/> unless <see cref="DisplayTitleIsFallback"/> is true.</summary>
    public required string DisplayTitle { get; init; }

    public bool DisplayTitleIsFallback { get; init; }

    /// <summary>Provider-native identifier (Xtream stream_id or series_id). Null for M3U entries.</summary>
    public string? ProviderItemId { get; init; }

    /// <summary>Hash of the credential-free stream locator. Null when the entry has no stream URL.</summary>
    public string? StreamKey { get; init; }

    public string? TvgId { get; init; }

    public string? TvgName { get; init; }

    /// <summary>Logo or artwork URL after secret redaction.</summary>
    public string? LogoUrl { get; init; }

    /// <summary>Raw group names. More than one when a provider assigns several categories.</summary>
    public IReadOnlyList<string> GroupNames { get; init; } = [];

    /// <summary>Raw extra attributes with lower-case keys. Only values allowed by <c>CatalogAttributePolicy</c> (X-03).</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = EmptyAttributes;

    /// <summary>
    /// Names of source attributes that were present but intentionally not stored (X-03). Values are never kept.
    /// Empty means nothing was withheld, which is different from the source sending no attributes.
    /// </summary>
    public IReadOnlyList<string> WithheldAttributeKeys { get; init; } = [];

    public int? Year { get; init; }

    public string? ContainerExtension { get; init; }
}
