namespace M3UAnalyzer.Domain.Imports;

/// <summary>Result of one section. <paramref name="ErrorSummary"/> must already be redacted.</summary>
public sealed record SectionResult(CatalogSection Section, SectionStatus Status, int ItemCount, string? ErrorSummary);
