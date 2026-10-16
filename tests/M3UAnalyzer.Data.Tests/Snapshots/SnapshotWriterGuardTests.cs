using System.Text.Json;
using M3UAnalyzer.Data.Snapshots;
using M3UAnalyzer.Data.Tests.TestSupport;
using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;
using M3UAnalyzer.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace M3UAnalyzer.Data.Tests.Snapshots;

[TestClass]
public sealed class SnapshotWriterGuardTests
{
    private static RawCatalogItem Item(int ordinal, IReadOnlyDictionary<string, string>? attributes = null, string? logoUrl = null, IReadOnlyList<string>? withheld = null) => new()
    {
        Ordinal = ordinal,
        ContentType = ContentType.Live,
        ContentTypeBasis = ContentTypeBasis.UrlInferred,
        RawTitle = $"Channel {ordinal}",
        DisplayTitle = $"Channel {ordinal}",
        Attributes = attributes ?? new Dictionary<string, string>(),
        LogoUrl = logoUrl,
        WithheldAttributeKeys = withheld ?? [],
    };

    [TestMethod]
    public async Task UnfilteredCredentialAttribute_IsRejected_AndNothingIsWritten()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);
        var items = new[]
        {
            Item(0),
            Item(1, new Dictionary<string, string>
            {
                ["catchup-source"] = "http://example.invalid/timeshift/alice/s3cret/{duration}/{start}/1.ts",
            }),
        };

        var exception = await Assert.ThrowsExactlyAsync<UnsafeCatalogDataException>(
            () => writer.AddItemsAsync(items, CancellationToken.None));

        Assert.IsFalse(exception.Message.Contains("alice", StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains("s3cret", StringComparison.Ordinal));
        Assert.AreEqual(0, writer.ItemsWritten);
        await using var db = database.CreateContext();
        Assert.AreEqual(0, await db.Items.CountAsync());
        Assert.AreEqual(0, await db.ItemGroups.CountAsync());
    }

    [TestMethod]
    public async Task UnsanitizedLogoUrl_IsRejected()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);

        await Assert.ThrowsExactlyAsync<UnsafeCatalogDataException>(
            () => writer.AddItemsAsync(
                [Item(0, logoUrl: "https://cdn.example.invalid/x.png?X-Amz-Signature=s3cret")],
                CancellationToken.None));

        await using var db = database.CreateContext();
        Assert.AreEqual(0, await db.Items.CountAsync());
    }

    [TestMethod]
    public async Task WithheldAttributeKeys_RoundTrip_AndEmptyIsNull()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);

        await writer.AddItemsAsync(
        [
            Item(0, withheld: ["catchup-source", "url-tvg"]),
            Item(1),
        ], CancellationToken.None);

        await using var db = database.CreateContext();
        Assert.AreEqual("[\"catchup-source\",\"url-tvg\"]", (await db.Items.SingleAsync(i => i.Ordinal == 0)).WithheldAttributeKeysJson);
        Assert.IsNull((await db.Items.SingleAsync(i => i.Ordinal == 1)).WithheldAttributeKeysJson);
    }

    [TestMethod]
    public async Task DiagnosticSummariesAndSamples_RedactCredentialUrlsBeforePersistence()
    {
        await using var database = await TempDatabase.CreateAsync();
        var snapshotId = await Seed.SnapshotAsync(database, await Seed.SourceAsync(database));
        var writer = new SnapshotWriter(database.ConnectionString, snapshotId);
        const string unsafeUrl = "http://example.invalid/timeshift/alice/s3cret/{duration}/{start}/1.ts";
        const string signedUrl = "https://cdn.example.invalid/logo.png?X-Amz-Signature=private-signature";

        await writer.SetSectionResultAsync(
            new SectionResult(CatalogSection.Live, SectionStatus.Failed, 0, $"Provider failed at {unsafeUrl}; logo {signedUrl}"),
            CancellationToken.None);
        await writer.AddWarningsAsync(
            [new ImportWarning(CatalogSection.Live, ImportWarningCodes.M3uNoGroup, 1, [$"No group at {unsafeUrl}; logo {signedUrl}"])],
            CancellationToken.None);

        await using var db = database.CreateContext();
        var summary = (await db.SnapshotSections.SingleAsync()).ErrorSummary!;
        var samples = JsonSerializer.Deserialize<string[]>((await db.ImportWarnings.SingleAsync()).SamplesJson)!;
        Assert.IsFalse(summary.Contains("alice", StringComparison.Ordinal));
        Assert.IsFalse(summary.Contains("s3cret", StringComparison.Ordinal));
        Assert.IsFalse(summary.Contains("private-signature", StringComparison.Ordinal));
        Assert.IsFalse(samples[0].Contains("alice", StringComparison.Ordinal));
        Assert.IsFalse(samples[0].Contains("s3cret", StringComparison.Ordinal));
        Assert.IsFalse(samples[0].Contains("private-signature", StringComparison.Ordinal));
    }
}
