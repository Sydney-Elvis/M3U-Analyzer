using M3UAnalyzer.Domain.Catalog;

namespace M3UAnalyzer.Domain.Imports;

/// <summary>Receives imported catalog data for one snapshot. Implementations persist it.</summary>
public interface ICatalogSink
{
    Task AddGroupsAsync(IReadOnlyList<CatalogGroup> groups, CancellationToken cancellationToken);
    Task AddItemsAsync(IReadOnlyList<RawCatalogItem> items, CancellationToken cancellationToken);
    Task SetSectionResultAsync(SectionResult result, CancellationToken cancellationToken);
    Task AddWarningsAsync(IReadOnlyList<ImportWarning> warnings, CancellationToken cancellationToken);
}
