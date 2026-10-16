namespace M3UAnalyzer.Domain.Catalog;

/// <summary>A provider group or category exactly as named by the source.</summary>
public sealed record CatalogGroup(ContentType ContentType, string RawName, string? ProviderCategoryId);
