using M3Undle.Core.M3u;
using M3Undle.Core.Providers;
using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;
using M3UAnalyzer.Domain.Security;

namespace M3UAnalyzer.Ingestion.M3u;

/// <summary>Counts reported by <see cref="M3uImporter.ImportAsync"/>.</summary>
public sealed record M3uImportSummary(int Entries, int Live, int Vod, int Series, int Unknown, int Groups, int WarningCount);

/// <summary>Imports an M3U playlist into an <see cref="ICatalogSink"/>. Never stores stream URLs.</summary>
public sealed class M3uImporter
{
    public const int BatchSize = 1000;

    public async Task<M3uImportSummary> ImportAsync(string playlistContent, ICatalogSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(playlistContent);
        ArgumentNullException.ThrowIfNull(sink);

        var document = new PlaylistParser().Parse(playlistContent, cancellationToken);

        var entries = document.Entries;
        var groups = new List<CatalogGroup>();
        var seenGroups = new HashSet<(ContentType, string)>();
        var items = new List<RawCatalogItem>();
        var collector = new ImportWarningCollector();

        int live = 0, vod = 0, series = 0, unknown = 0;

        for (var i = 0; i < entries.Count; i++)
        {
            if (i % BatchSize == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var entry = entries[i];
            var attributes = M3uAttributeParser.Parse(entry.MetadataLines);
            var (type, basis) = ContentTypeInference.FromUrl(entry.Url);

            if (type == ContentType.Vod)
            {
                vod++;
            }
            else if (type == ContentType.Series)
            {
                series++;
            }
            else if (type == ContentType.Unknown)
            {
                unknown++;
            }
            else
            {
                live++;
            }

            string? tvgId;
            string? tvgName;
            string? logo;
            string? streamKey;

            if (string.IsNullOrWhiteSpace(entry.Url))
            {
                tvgId = NullIfBlank(attributes.TryGetValue("tvg-id", out var v1) ? v1 : null);
                tvgName = NullIfBlank(attributes.TryGetValue("tvg-name", out var v2) ? v2 : null);
                logo = NullIfBlank(attributes.TryGetValue("tvg-logo", out var v3) ? v3 : null);
                streamKey = null;
            }
            else
            {
                var channel = ProviderChannelNormalizer.ParseEntry(entry);
                tvgId = channel.TvgId;
                tvgName = channel.TvgName;
                logo = channel.LogoUrl;
                streamKey = StreamKeys.Compute(type, ProviderChannelNormalizer.BuildStreamLocator(entry.Url));
            }

            string? group;
            if (!string.IsNullOrWhiteSpace(entry.Group))
            {
                group = entry.Group.Trim();
            }
            else if (attributes.TryGetValue(M3uAttributeParser.ExtGrpKey, out var extgrp) && !string.IsNullOrWhiteSpace(extgrp))
            {
                group = extgrp.Trim();
            }
            else
            {
                group = null;
            }

            var rawTitle = entry.Title;
            string displayTitle;
            bool fallback;
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                displayTitle = tvgName ?? $"(untitled #{i})";
                fallback = true;
            }
            else
            {
                displayTitle = rawTitle.Trim();
                fallback = false;
            }

            // Warnings in order
            if (streamKey is null)
            {
                collector.Add(CatalogSection.Live, ImportWarningCodes.M3uMissingUrl, displayTitle);
            }
            if (type == ContentType.Live && basis == ContentTypeBasis.Defaulted)
            {
                collector.Add(CatalogSection.Live, ImportWarningCodes.M3uTypeDefaulted, displayTitle);
            }
            if (fallback)
            {
                collector.Add(CatalogSection.Live, ImportWarningCodes.M3uEmptyTitle, displayTitle);
            }
            if (group is null)
            {
                collector.Add(CatalogSection.Live, ImportWarningCodes.M3uNoGroup, displayTitle);
            }

            if (group is not null)
            {
                var key = (type, group);
                if (seenGroups.Add(key))
                {
                    groups.Add(new CatalogGroup(type, group, null));
                }
            }

            var filtered = CatalogAttributePolicy.Filter(attributes);

            items.Add(new RawCatalogItem
            {
                Ordinal = i,
                ContentType = type,
                ContentTypeBasis = basis,
                RawTitle = rawTitle,
                DisplayTitle = displayTitle,
                DisplayTitleIsFallback = fallback,
                StreamKey = streamKey,
                TvgId = tvgId,
                TvgName = tvgName,
                LogoUrl = CatalogAttributePolicy.SanitizeLogoUrl(logo),
                GroupNames = group is null ? Array.Empty<string>() : new[] { group },
                Attributes = filtered.Kept,
                WithheldAttributeKeys = filtered.WithheldKeys,
            });
        }

        // Sink calls
        await sink.AddGroupsAsync(groups, cancellationToken);

        for (var offset = 0; offset < items.Count; offset += BatchSize)
        {
            var batch = items.Skip(offset).Take(BatchSize).ToList();
            await sink.AddItemsAsync(batch, cancellationToken);
        }

        await sink.SetSectionResultAsync(new SectionResult(CatalogSection.Live, SectionStatus.Ok, live, null), cancellationToken);
        await sink.SetSectionResultAsync(new SectionResult(CatalogSection.Vod, SectionStatus.Ok, vod, null), cancellationToken);
        await sink.SetSectionResultAsync(new SectionResult(CatalogSection.Series, SectionStatus.Ok, series, null), cancellationToken);
        await sink.SetSectionResultAsync(new SectionResult(CatalogSection.Episodes, SectionStatus.NotSupported, 0, null), cancellationToken);

        await sink.AddWarningsAsync(collector.ToWarnings(), cancellationToken);

        return new M3uImportSummary(entries.Count, live, vod, series, unknown, groups.Count, collector.TotalCount);
    }

    private static string? NullIfBlank(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return value.Trim();
    }
}
