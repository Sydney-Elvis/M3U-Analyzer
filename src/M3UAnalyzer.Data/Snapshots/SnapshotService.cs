using M3UAnalyzer.Data.Entities;
using M3UAnalyzer.Domain.Imports;
using M3UAnalyzer.Domain.Security;
using M3UAnalyzer.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace M3UAnalyzer.Data.Snapshots;

/// <summary>How an import ended, as reported by the orchestrator.</summary>
public sealed record SnapshotFinish(long? BytesFetched = null, string? FatalError = null, bool Cancelled = false);

/// <summary>Owns snapshot status transitions, the current-snapshot pointer, retention, and crash recovery.</summary>
public sealed class SnapshotService(IDbContextFactory<AnalyzerDbContext> contextFactory, TimeProvider timeProvider)
{
    public const string InterruptedMessage = "Interrupted: the application stopped during this import.";
    public const int FailedHistoryToKeep = 10;
    private const int MaxErrorSummaryLength = 1000;

    public static SnapshotStatus DecideStatus(IReadOnlyCollection<SnapshotSectionEntity> sections, SnapshotFinish finish)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(finish);

        if (finish.Cancelled)
        {
            return SnapshotStatus.Cancelled;
        }
        if (finish.FatalError is not null)
        {
            return SnapshotStatus.Failed;
        }

        var attempted = sections
            .Where(section => section.Section is CatalogSection.Live or CatalogSection.Vod or CatalogSection.Series)
            .Where(section => section.Status is SectionStatus.Ok or SectionStatus.Failed)
            .ToArray();

        if (attempted.Length == 0 || attempted.All(section => section.Status == SectionStatus.Failed))
        {
            return SnapshotStatus.Failed;
        }
        return attempted.Any(section => section.Status == SectionStatus.Failed)
            ? SnapshotStatus.Partial
            : SnapshotStatus.Complete;
    }

    public async Task<string> BeginAsync(string sourceId, string? coreReference, int rulesVersion, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        await using var db = contextFactory.CreateDbContext();
        if (!await db.Sources.AnyAsync(source => source.Id == sourceId, cancellationToken))
        {
            throw new InvalidOperationException($"Source '{sourceId}' does not exist.");
        }

        var id = Guid.NewGuid().ToString("D");
        db.Snapshots.Add(new SnapshotEntity
        {
            Id = id,
            SourceId = sourceId,
            Status = SnapshotStatus.Importing,
            StartedUtc = timeProvider.GetUtcNow().UtcDateTime,
            CoreReference = coreReference,
            RulesVersion = rulesVersion,
        });
        await db.SaveChangesAsync(cancellationToken);
        return id;
    }

    public async Task<SnapshotStatus> FinishAsync(string snapshotId, SnapshotFinish finish, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);
        ArgumentNullException.ThrowIfNull(finish);

        await using var db = contextFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var snapshot = await db.Snapshots.SingleOrDefaultAsync(item => item.Id == snapshotId, cancellationToken);
        if (snapshot is null || snapshot.Status != SnapshotStatus.Importing)
        {
            throw new InvalidOperationException("Only an importing snapshot can be finished.");
        }

        var sections = await db.SnapshotSections
            .Where(section => section.SnapshotId == snapshotId)
            .ToListAsync(cancellationToken);
        var status = DecideStatus(sections, finish);
        if (status is SnapshotStatus.Failed or SnapshotStatus.Cancelled)
        {
            await DeleteCatalogRowsAsync(db, snapshotId, cancellationToken);
        }
        else
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CatalogGroups SET ItemCount = (SELECT COUNT(*) FROM ItemGroups WHERE ItemGroups.GroupId = CatalogGroups.Id) WHERE SnapshotId = {snapshotId};",
                cancellationToken);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        snapshot.Status = status;
        snapshot.FinishedUtc = now;
        snapshot.DurationMs = (long)(now - snapshot.StartedUtc).TotalMilliseconds;
        snapshot.BytesFetched = finish.BytesFetched;
        snapshot.ErrorSummary = Truncate(TextRedactor.Redact(finish.Cancelled ? "Cancelled by user." : finish.FatalError));

        var source = await db.Sources.SingleAsync(item => item.Id == snapshot.SourceId, cancellationToken);
        if (status == SnapshotStatus.Complete)
        {
            source.CurrentSnapshotId = snapshotId;
        }
        else if (status == SnapshotStatus.Partial)
        {
            if (source.CurrentSnapshotId is null)
            {
                source.CurrentSnapshotId = snapshotId;
            }
            else
            {
                var currentSnapshot = await db.Snapshots
                    .SingleOrDefaultAsync(item => item.Id == source.CurrentSnapshotId, cancellationToken);
                if (currentSnapshot?.Status == SnapshotStatus.Partial)
                {
                    source.CurrentSnapshotId = snapshotId;
                }
            }
        }

        source.UpdatedUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return status;
    }

    public async Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var db = contextFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var importing = await db.Snapshots
            .Where(snapshot => snapshot.Status == SnapshotStatus.Importing)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var snapshot in importing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DeleteCatalogRowsAsync(db, snapshot.Id, cancellationToken);
            snapshot.Status = SnapshotStatus.Failed;
            snapshot.FinishedUtc = now;
            snapshot.ErrorSummary = InterruptedMessage;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return importing.Count;
    }

    public async Task<int> ApplyRetentionAsync(string sourceId, int keepSuccessful, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentOutOfRangeException.ThrowIfNegative(keepSuccessful);

        await using var db = contextFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var currentSnapshotId = await db.Sources
            .Where(source => source.Id == sourceId)
            .Select(source => source.CurrentSnapshotId)
            .SingleOrDefaultAsync(cancellationToken);

        var candidates = db.Snapshots
            .Where(snapshot => snapshot.SourceId == sourceId
                && snapshot.Status != SnapshotStatus.Importing
                && snapshot.Id != currentSnapshotId);
        var successfulToDelete = await candidates
            .Where(snapshot => snapshot.Status == SnapshotStatus.Complete || snapshot.Status == SnapshotStatus.Partial)
            .OrderByDescending(snapshot => snapshot.StartedUtc)
            .ThenByDescending(snapshot => snapshot.Id)
            .Skip(keepSuccessful)
            .Select(snapshot => snapshot.Id)
            .ToListAsync(cancellationToken);
        var failedToDelete = await candidates
            .Where(snapshot => snapshot.Status == SnapshotStatus.Failed || snapshot.Status == SnapshotStatus.Cancelled)
            .OrderByDescending(snapshot => snapshot.StartedUtc)
            .ThenByDescending(snapshot => snapshot.Id)
            .Skip(FailedHistoryToKeep)
            .Select(snapshot => snapshot.Id)
            .ToListAsync(cancellationToken);

        var idsToDelete = successfulToDelete.Concat(failedToDelete).ToArray();
        foreach (var id in idsToDelete)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DeleteCatalogRowsAsync(db, id, cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ImportWarnings WHERE SnapshotId = {id};", cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM SnapshotSections WHERE SnapshotId = {id};", cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Snapshots WHERE Id = {id};", cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return idsToDelete.Length;
    }

    /// <summary>
    /// Deletes every catalog row of a snapshot, child tables first. EVERY later table keyed by snapshot or item
    /// (EPG, derived attributes, search index, ...) must be added here by the card that creates it.
    /// </summary>
    internal static async Task DeleteCatalogRowsAsync(AnalyzerDbContext db, string snapshotId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM ItemGroups WHERE ItemId IN (SELECT Id FROM Items WHERE SnapshotId = {snapshotId});",
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM SeriesEpisodeLoads WHERE SeriesItemId IN (SELECT Id FROM Items WHERE SnapshotId = {snapshotId});",
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM Episodes WHERE SnapshotId = {snapshotId};",
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM Items WHERE SnapshotId = {snapshotId};",
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM CatalogGroups WHERE SnapshotId = {snapshotId};",
            cancellationToken);
    }

    private static string? Truncate(string? value) =>
        value is { Length: > MaxErrorSummaryLength } ? value[..MaxErrorSummaryLength] : value;
}
