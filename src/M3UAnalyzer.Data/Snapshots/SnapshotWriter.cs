using System.Text.Json;
using System.Text.RegularExpressions;
using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;
using M3UAnalyzer.Domain.Security;
using Microsoft.Data.Sqlite;

namespace M3UAnalyzer.Data.Snapshots;

/// <summary>Persists imported catalog data for one snapshot using batched SQLite inserts.</summary>
public sealed class SnapshotWriter : ICatalogSink
{
    private static readonly Regex UrlQueryRegex = new(
        "(?<url>[A-Za-z][A-Za-z0-9+.\\-]*://[^\\s\"'<>?#]*)\\?[^\\s\"'<>#]*",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private const string GroupInsertSql = """
        INSERT INTO CatalogGroups (SnapshotId, ContentType, RawName, ProviderCategoryId, ItemCount)
        VALUES ($snapshot, $type, $name, $category, 0)
        ON CONFLICT (SnapshotId, ContentType, RawName) DO UPDATE SET ProviderCategoryId = COALESCE(CatalogGroups.ProviderCategoryId, excluded.ProviderCategoryId)
        RETURNING Id;
        """;

    private readonly string _connectionString;
    private readonly string _snapshotId;

    public SnapshotWriter(string connectionString, string snapshotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);

        _connectionString = connectionString;
        _snapshotId = snapshotId;
    }

    public int ItemsWritten { get; private set; }

    public async Task AddGroupsAsync(IReadOnlyList<CatalogGroup> groups, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = GroupInsertSql;
        command.Parameters.AddWithValue("$snapshot", _snapshotId);
        command.Parameters.Add("$type", SqliteType.Text);
        command.Parameters.Add("$name", SqliteType.Text);
        command.Parameters.Add("$category", SqliteType.Text);
        command.Prepare();

        var groupIds = new Dictionary<(ContentType Type, string Name), long>();
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = await GetOrCreateGroupIdAsync(command, group.ContentType, group.RawName, group.ProviderCategoryId, groupIds, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddItemsAsync(IReadOnlyList<RawCatalogItem> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            CatalogAttributePolicy.EnsurePersistable(item);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var groupCommand = connection.CreateCommand();
        groupCommand.Transaction = transaction;
        groupCommand.CommandText = GroupInsertSql;
        groupCommand.Parameters.AddWithValue("$snapshot", _snapshotId);
        groupCommand.Parameters.Add("$type", SqliteType.Text);
        groupCommand.Parameters.Add("$name", SqliteType.Text);
        groupCommand.Parameters.Add("$category", SqliteType.Text);
        groupCommand.Prepare();

        await using var itemCommand = connection.CreateCommand();
        itemCommand.Transaction = transaction;
        itemCommand.CommandText = """
            INSERT INTO Items (
                SnapshotId, Ordinal, ContentType, ContentTypeBasis, RawTitle, DisplayTitle, DisplayTitleIsFallback,
                ProviderItemId, StreamKey, TvgId, TvgName, LogoUrl, AttributesJson, WithheldAttributeKeysJson, Year, ContainerExtension)
            VALUES (
                $snapshot, $ordinal, $type, $basis, $rawTitle, $displayTitle, $isFallback,
                $providerItemId, $streamKey, $tvgId, $tvgName, $logoUrl, $attributesJson, $withheldKeysJson, $year, $containerExtension)
            RETURNING Id;
            """;
        itemCommand.Parameters.AddWithValue("$snapshot", _snapshotId);
        itemCommand.Parameters.Add("$ordinal", SqliteType.Integer);
        itemCommand.Parameters.Add("$type", SqliteType.Text);
        itemCommand.Parameters.Add("$basis", SqliteType.Text);
        itemCommand.Parameters.Add("$rawTitle", SqliteType.Text);
        itemCommand.Parameters.Add("$displayTitle", SqliteType.Text);
        itemCommand.Parameters.Add("$isFallback", SqliteType.Integer);
        itemCommand.Parameters.Add("$providerItemId", SqliteType.Text);
        itemCommand.Parameters.Add("$streamKey", SqliteType.Text);
        itemCommand.Parameters.Add("$tvgId", SqliteType.Text);
        itemCommand.Parameters.Add("$tvgName", SqliteType.Text);
        itemCommand.Parameters.Add("$logoUrl", SqliteType.Text);
        itemCommand.Parameters.Add("$attributesJson", SqliteType.Text);
        itemCommand.Parameters.Add("$withheldKeysJson", SqliteType.Text);
        itemCommand.Parameters.Add("$year", SqliteType.Integer);
        itemCommand.Parameters.Add("$containerExtension", SqliteType.Text);
        itemCommand.Prepare();

        await using var itemGroupCommand = connection.CreateCommand();
        itemGroupCommand.Transaction = transaction;
        itemGroupCommand.CommandText = "INSERT OR IGNORE INTO ItemGroups (ItemId, GroupId) VALUES ($item, $group);";
        itemGroupCommand.Parameters.Add("$item", SqliteType.Integer);
        itemGroupCommand.Parameters.Add("$group", SqliteType.Integer);
        itemGroupCommand.Prepare();

        var groupIds = new Dictionary<(ContentType Type, string Name), long>();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Set(itemCommand, "$ordinal", item.Ordinal);
            Set(itemCommand, "$type", item.ContentType.ToString());
            Set(itemCommand, "$basis", item.ContentTypeBasis.ToString());
            Set(itemCommand, "$rawTitle", item.RawTitle);
            Set(itemCommand, "$displayTitle", item.DisplayTitle);
            Set(itemCommand, "$isFallback", item.DisplayTitleIsFallback ? 1 : 0);
            Set(itemCommand, "$providerItemId", item.ProviderItemId);
            Set(itemCommand, "$streamKey", item.StreamKey);
            Set(itemCommand, "$tvgId", item.TvgId);
            Set(itemCommand, "$tvgName", item.TvgName);
            Set(itemCommand, "$logoUrl", item.LogoUrl);
            Set(itemCommand, "$attributesJson", item.Attributes.Count == 0 ? null : JsonSerializer.Serialize(item.Attributes));
            Set(itemCommand, "$withheldKeysJson", item.WithheldAttributeKeys.Count == 0 ? null : JsonSerializer.Serialize(item.WithheldAttributeKeys));
            Set(itemCommand, "$year", item.Year);
            Set(itemCommand, "$containerExtension", item.ContainerExtension);

            var itemId = Convert.ToInt64(await itemCommand.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            foreach (var groupName in item.GroupNames)
            {
                var groupId = await GetOrCreateGroupIdAsync(groupCommand, item.ContentType, groupName, null, groupIds, cancellationToken);
                Set(itemGroupCommand, "$item", itemId);
                Set(itemGroupCommand, "$group", groupId);
                await itemGroupCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        ItemsWritten += items.Count;
    }

    public async Task SetSectionResultAsync(SectionResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SnapshotSections (SnapshotId, Section, Status, ItemCount, ErrorSummary)
            VALUES ($snapshot, $section, $status, $count, $error)
            ON CONFLICT (SnapshotId, Section) DO UPDATE SET Status = excluded.Status, ItemCount = excluded.ItemCount, ErrorSummary = excluded.ErrorSummary;
            """;
        command.Parameters.AddWithValue("$snapshot", _snapshotId);
        command.Parameters.AddWithValue("$section", result.Section.ToString());
        command.Parameters.AddWithValue("$status", result.Status.ToString());
        command.Parameters.AddWithValue("$count", result.ItemCount);
        command.Parameters.AddWithValue("$error", (object?)SanitizeDiagnosticText(result.ErrorSummary) ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddWarningsAsync(IReadOnlyList<ImportWarning> warnings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        if (warnings.Count == 0)
        {
            return;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ImportWarnings (SnapshotId, Section, Code, Count, SamplesJson)
            VALUES ($snapshot, $section, $code, $count, $samples);
            """;
        command.Parameters.AddWithValue("$snapshot", _snapshotId);
        command.Parameters.Add("$section", SqliteType.Text);
        command.Parameters.Add("$code", SqliteType.Text);
        command.Parameters.Add("$count", SqliteType.Integer);
        command.Parameters.Add("$samples", SqliteType.Text);
        command.Prepare();

        foreach (var warning in warnings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Set(command, "$section", warning.Section.ToString());
            Set(command, "$code", warning.Code);
            Set(command, "$count", warning.Count);
            var safeSamples = warning.Samples.Select(SanitizeDiagnosticText).ToArray();
            Set(command, "$samples", JsonSerializer.Serialize(safeSamples));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<long> GetOrCreateGroupIdAsync(
        SqliteCommand command,
        ContentType contentType,
        string rawName,
        string? providerCategoryId,
        Dictionary<(ContentType Type, string Name), long> groupIds,
        CancellationToken cancellationToken)
    {
        var key = (contentType, rawName);
        if (groupIds.TryGetValue(key, out var groupId) && providerCategoryId is null)
        {
            return groupId;
        }

        Set(command, "$type", contentType.ToString());
        Set(command, "$name", rawName);
        Set(command, "$category", providerCategoryId);
        groupId = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        groupIds[key] = groupId;
        return groupId;
    }

    private static void Set(SqliteCommand command, string parameterName, object? value)
    {
        command.Parameters[parameterName].Value = value ?? DBNull.Value;
    }

    private static string? SanitizeDiagnosticText(string? text)
    {
        var redacted = TextRedactor.Redact(text);
        return redacted is null ? null : UrlQueryRegex.Replace(redacted, "$1");
    }
}
