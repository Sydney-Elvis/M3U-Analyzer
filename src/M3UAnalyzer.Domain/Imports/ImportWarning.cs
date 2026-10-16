namespace M3UAnalyzer.Domain.Imports;

/// <summary>Aggregated warning: one row per section and code, with a few redacted samples.</summary>
public sealed record ImportWarning(CatalogSection Section, string Code, int Count, IReadOnlyList<string> Samples);
