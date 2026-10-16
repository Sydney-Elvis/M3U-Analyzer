using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;

namespace M3UAnalyzer.Ingestion.Tests.TestDoubles;

/// <summary>In-memory <see cref="ICatalogSink"/> that records every call in order.</summary>
public sealed class RecordingCatalogSink : ICatalogSink
{
    public List<string> Calls { get; } = [];
    public List<CatalogGroup> Groups { get; } = [];
    public List<RawCatalogItem> Items { get; } = [];
    public List<int> ItemBatchSizes { get; } = [];
    public Dictionary<CatalogSection, SectionResult> Sections { get; } = [];
    public List<ImportWarning> Warnings { get; } = [];

    public Task AddGroupsAsync(IReadOnlyList<CatalogGroup> groups, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(AddGroupsAsync));
        Groups.AddRange(groups);
        return Task.CompletedTask;
    }

    public Task AddItemsAsync(IReadOnlyList<RawCatalogItem> items, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(AddItemsAsync));
        ItemBatchSizes.Add(items.Count);
        Items.AddRange(items);
        return Task.CompletedTask;
    }

    public Task SetSectionResultAsync(SectionResult result, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(SetSectionResultAsync));
        Sections[result.Section] = result;
        return Task.CompletedTask;
    }

    public Task AddWarningsAsync(IReadOnlyList<ImportWarning> warnings, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(AddWarningsAsync));
        Warnings.AddRange(warnings);
        return Task.CompletedTask;
    }
}
