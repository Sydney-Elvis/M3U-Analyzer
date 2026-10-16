namespace M3UAnalyzer.Domain.Catalog;

/// <summary>How an item's <see cref="ContentType"/> was determined.</summary>
public enum ContentTypeBasis
{
    /// <summary>The provider stated the type explicitly (for example, the Xtream endpoint the item came from).</summary>
    ProviderDeclared,
    /// <summary>Inferred from positive evidence in the stream URL (path segment, query, or extension).</summary>
    UrlInferred,
    /// <summary>No evidence; a default was applied.</summary>
    Defaulted,
}
