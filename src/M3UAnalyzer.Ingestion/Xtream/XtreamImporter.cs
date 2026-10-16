using System.Globalization;
using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;
using M3UAnalyzer.Domain.Security;
using M3UAnalyzer.Domain.Sources;
using M3UAnalyzer.Ingestion.Fetching;

namespace M3UAnalyzer.Ingestion.Xtream;

public sealed class XtreamAuthenticationException() : Exception("The provider rejected the Xtream credentials.");

public sealed record XtreamImportOptions(bool IncludeVod, bool IncludeSeries);

public sealed record XtreamImportSummary(int Live, int Vod, int Series, int WarningCount);

public sealed class XtreamImporter(IXtreamApiClient client)
{
    public const int BatchSize = 1000;

    public async Task<XtreamImportSummary> ImportAsync(
        XtreamCredentials credentials,
        XtreamImportOptions options,
        FetchPolicy policy,
        ICatalogSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(sink);

        var warnings = new ImportWarningCollector();
        XtreamAccountInfo? account;
        try
        {
            account = await client.GetAccountInfoAsync(credentials, policy, cancellationToken);
        }
        catch (FetchFailedException)
        {
            account = null;
        }

        if (account is null)
        {
            warnings.Add(CatalogSection.Live, ImportWarningCodes.XtreamAccountInfoUnavailable);
        }
        else if (!account.Authenticated)
        {
            throw new XtreamAuthenticationException();
        }

        var live = await ImportSectionAsync(
            credentials,
            policy,
            sink,
            warnings,
            cancellationToken,
            CatalogSection.Live,
            XtreamCatalogKind.Live,
            ContentType.Live,
            client.GetLiveStreamsAsync,
            static item => item.StreamId,
            static item => item.Name,
            static item => item.CategoryIds,
            static item => item.IconUrl,
            static item => item.EpgChannelId,
            static item => null,
            static item => null,
            static item =>
            {
                var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                if (item.Number is { } number)
                {
                    attributes["num"] = number.ToString(CultureInfo.InvariantCulture);
                }
                if (item.HasArchive is { } hasArchive)
                {
                    attributes["tv_archive"] = hasArchive ? "1" : "0";
                }
                return attributes;
            });

        var vod = options.IncludeVod
            ? await ImportSectionAsync(
                credentials,
                policy,
                sink,
                warnings,
                cancellationToken,
                CatalogSection.Vod,
                XtreamCatalogKind.Vod,
                ContentType.Vod,
                client.GetVodStreamsAsync,
                static item => item.StreamId,
                static item => item.Name,
                static item => item.CategoryIds,
                static item => item.IconUrl,
                static _ => null,
                static item => item.Year,
                static item => item.ContainerExtension,
                static item =>
                {
                    var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (item.Rating is not null)
                    {
                        attributes["rating"] = item.Rating;
                    }
                    if (item.TmdbId is not null)
                    {
                        attributes["tmdb"] = item.TmdbId;
                    }
                    return attributes;
                })
            : await MarkNotRequestedAsync(sink, CatalogSection.Vod, cancellationToken);

        var series = options.IncludeSeries
            ? await ImportSectionAsync(
                credentials,
                policy,
                sink,
                warnings,
                cancellationToken,
                CatalogSection.Series,
                XtreamCatalogKind.Series,
                ContentType.Series,
                client.GetSeriesAsync,
                static item => item.SeriesId,
                static item => item.Name,
                static item => item.CategoryIds,
                static item => item.CoverUrl,
                static _ => null,
                static item => item.Year,
                static _ => null,
                static item =>
                {
                    var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (item.TmdbId is not null)
                    {
                        attributes["tmdb"] = item.TmdbId;
                    }
                    if (item.LastModifiedEpoch > 0)
                    {
                        attributes["last_modified"] = item.LastModifiedEpoch.ToString(CultureInfo.InvariantCulture);
                    }
                    return attributes;
                })
            : await MarkNotRequestedAsync(sink, CatalogSection.Series, cancellationToken);

        await sink.SetSectionResultAsync(
            new SectionResult(CatalogSection.Episodes, SectionStatus.NotRequested, 0, null),
            cancellationToken);
        await sink.AddWarningsAsync(warnings.ToWarnings(), cancellationToken);
        return new XtreamImportSummary(live, vod, series, warnings.TotalCount);
    }

    private static async Task<int> MarkNotRequestedAsync(
        ICatalogSink sink,
        CatalogSection section,
        CancellationToken cancellationToken)
    {
        await sink.SetSectionResultAsync(new SectionResult(section, SectionStatus.NotRequested, 0, null), cancellationToken);
        return 0;
    }

    private async Task<int> ImportSectionAsync<T>(
        XtreamCredentials credentials,
        FetchPolicy policy,
        ICatalogSink sink,
        ImportWarningCollector warnings,
        CancellationToken cancellationToken,
        CatalogSection section,
        XtreamCatalogKind kind,
        ContentType contentType,
        Func<XtreamCredentials, FetchPolicy, CancellationToken, Task<XtreamParseResult<T>>> fetchItems,
        Func<T, int> getId,
        Func<T, string> getName,
        Func<T, IReadOnlyList<int>> getCategoryIds,
        Func<T, string?> getArtwork,
        Func<T, string?> getTvgId,
        Func<T, int?> getYear,
        Func<T, string?> getContainerExtension,
        Func<T, Dictionary<string, string>> getAttributes)
    {
        XtreamParseResult<XtreamCategory> categories;
        XtreamParseResult<T> itemResult;
        try
        {
            categories = await client.GetCategoriesAsync(credentials, kind, policy, cancellationToken);
            itemResult = await fetchItems(credentials, policy, cancellationToken);
        }
        catch (FetchFailedException exception)
        {
            await sink.SetSectionResultAsync(
                new SectionResult(section, SectionStatus.Failed, 0, exception.Message),
                cancellationToken);
            return 0;
        }

        if (itemResult.DocumentMalformed)
        {
            await sink.SetSectionResultAsync(
                new SectionResult(section, SectionStatus.Failed, 0, $"The provider returned an unreadable {section.ToString().ToLowerInvariant()} list."),
                cancellationToken);
            return 0;
        }

        if (categories.DocumentMalformed)
        {
            warnings.Add(section, ImportWarningCodes.XtreamMalformedDocument, $"{section.ToString().ToLowerInvariant()} categories");
        }

        AddRepeatedWarnings(warnings, section, ImportWarningCodes.XtreamInvalidItem, categories.SkippedInvalid + itemResult.SkippedInvalid);

        var categoryNames = categories.Items.ToDictionary(category => category.CategoryId, category => category.Name);
        var groups = categories.Items
            .Select(category => new CatalogGroup(
                contentType,
                category.Name,
                category.CategoryId.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        await sink.AddGroupsAsync(groups, cancellationToken);

        var batch = new List<RawCatalogItem>(BatchSize);
        for (var index = 0; index < itemResult.Items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = itemResult.Items[index];
            var groupNames = new List<string>();
            foreach (var categoryId in getCategoryIds(item))
            {
                if (categoryNames.TryGetValue(categoryId, out var categoryName))
                {
                    groupNames.Add(categoryName);
                }
                else
                {
                    var placeholder = $"(category {categoryId.ToString(CultureInfo.InvariantCulture)})";
                    groupNames.Add(placeholder);
                    warnings.Add(section, ImportWarningCodes.XtreamUnknownCategory, placeholder);
                }
            }

            var name = getName(item);
            var isFallback = string.IsNullOrWhiteSpace(name);
            var attributes = CatalogAttributePolicy.Filter(getAttributes(item));
            batch.Add(new RawCatalogItem
            {
                Ordinal = index,
                ContentType = contentType,
                ContentTypeBasis = ContentTypeBasis.ProviderDeclared,
                RawTitle = name,
                DisplayTitle = isFallback ? $"(untitled #{index})" : name.Trim(),
                DisplayTitleIsFallback = isFallback,
                ProviderItemId = getId(item).ToString(CultureInfo.InvariantCulture),
                StreamKey = contentType == ContentType.Series
                    ? null
                    : StreamKeys.Compute(contentType, "xtream:" + getId(item).ToString(CultureInfo.InvariantCulture)),
                TvgId = getTvgId(item),
                LogoUrl = CatalogAttributePolicy.SanitizeLogoUrl(getArtwork(item)),
                GroupNames = groupNames,
                Attributes = attributes.Kept,
                WithheldAttributeKeys = attributes.WithheldKeys,
                Year = getYear(item),
                ContainerExtension = getContainerExtension(item),
            });

            if (isFallback)
            {
                warnings.Add(section, ImportWarningCodes.XtreamEmptyTitle);
            }

            if (batch.Count == BatchSize)
            {
                await sink.AddItemsAsync(batch, cancellationToken);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await sink.AddItemsAsync(batch, cancellationToken);
        }

        await sink.SetSectionResultAsync(
            new SectionResult(section, SectionStatus.Ok, itemResult.Items.Count, null),
            cancellationToken);
        return itemResult.Items.Count;
    }

    private static void AddRepeatedWarnings(ImportWarningCollector warnings, CatalogSection section, string code, int count)
    {
        for (var index = 0; index < count; index++)
        {
            warnings.Add(section, code);
        }
    }
}
