namespace M3UAnalyzer.Domain.Security;

/// <summary>Result of <see cref="CatalogAttributePolicy.Filter"/>.</summary>
public sealed record CatalogAttributeFilterResult(IReadOnlyDictionary<string, string> Kept, IReadOnlyList<string> WithheldKeys);
