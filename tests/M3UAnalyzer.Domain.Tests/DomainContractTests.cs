using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Imports;
using M3UAnalyzer.Domain.Sources;

namespace M3UAnalyzer.Domain.Tests;

[TestClass]
public sealed class DomainContractTests
{
    [TestMethod]
    public void Enums_HaveExactMembersInOrder()
    {
        CollectionAssert.AreEqual(new[] { "Live", "Vod", "Series", "Unknown" }, Enum.GetNames<ContentType>());
        CollectionAssert.AreEqual(new[] { "ProviderDeclared", "UrlInferred", "Defaulted" }, Enum.GetNames<ContentTypeBasis>());
        CollectionAssert.AreEqual(new[] { "Live", "Vod", "Series", "Episodes", "Epg" }, Enum.GetNames<CatalogSection>());
        CollectionAssert.AreEqual(new[] { "Ok", "Failed", "NotSupported", "NotRequested" }, Enum.GetNames<SectionStatus>());
        CollectionAssert.AreEqual(new[] { "Importing", "Complete", "Partial", "Failed", "Cancelled" }, Enum.GetNames<SnapshotStatus>());
        CollectionAssert.AreEqual(new[] { "M3uUrl", "M3uFile", "Xtream" }, Enum.GetNames<SourceKind>());
    }

    [TestMethod]
    public void RawCatalogItem_RequiredAndDefaultValues()
    {
        var item = new RawCatalogItem
        {
            Ordinal = 7,
            ContentType = ContentType.Live,
            ContentTypeBasis = ContentTypeBasis.UrlInferred,
            RawTitle = "CNN HD",
            DisplayTitle = "CNN HD",
        };

        Assert.AreEqual(7, item.Ordinal);
        Assert.AreEqual(ContentType.Live, item.ContentType);
        Assert.AreEqual(ContentTypeBasis.UrlInferred, item.ContentTypeBasis);
        Assert.AreEqual("CNN HD", item.RawTitle);
        Assert.AreEqual("CNN HD", item.DisplayTitle);
        Assert.IsFalse(item.DisplayTitleIsFallback);
        Assert.IsNull(item.ProviderItemId);
        Assert.IsNull(item.StreamKey);
        Assert.IsNull(item.TvgId);
        Assert.IsNull(item.TvgName);
        Assert.IsNull(item.LogoUrl);
        Assert.IsNull(item.Year);
        Assert.IsNull(item.ContainerExtension);
        Assert.IsNotNull(item.GroupNames);
        Assert.AreEqual(0, item.GroupNames.Count);
        Assert.IsNotNull(item.Attributes);
        Assert.AreEqual(0, item.Attributes.Count);
    }

    [TestMethod]
    public void RawCatalogItem_WithExpression_ChangesOnlyTheGivenProperty()
    {
        var item = new RawCatalogItem
        {
            Ordinal = 0,
            ContentType = ContentType.Vod,
            ContentTypeBasis = ContentTypeBasis.ProviderDeclared,
            RawTitle = "",
            DisplayTitle = "(untitled #0)",
            DisplayTitleIsFallback = true,
            ProviderItemId = "456",
            GroupNames = ["Movies", "New"],
            Attributes = new Dictionary<string, string> { ["tvg-country"] = "US" },
            Year = 2020,
            ContainerExtension = "mkv",
        };

        var changed = item with { Year = 2021 };

        Assert.AreEqual(2021, changed.Year);
        Assert.AreEqual(2020, item.Year);
        Assert.AreEqual("456", changed.ProviderItemId);
        Assert.IsTrue(changed.DisplayTitleIsFallback);
        CollectionAssert.AreEqual(new[] { "Movies", "New" }, changed.GroupNames.ToArray());
        Assert.AreEqual("US", changed.Attributes["tvg-country"]);
    }

    [TestMethod]
    public void PositionalRecords_HaveValueEquality()
    {
        Assert.AreEqual(new CatalogGroup(ContentType.Live, "News", "12"), new CatalogGroup(ContentType.Live, "News", "12"));
        Assert.AreNotEqual(new CatalogGroup(ContentType.Live, "News", "12"), new CatalogGroup(ContentType.Vod, "News", "12"));
        Assert.AreEqual(
            new SectionResult(CatalogSection.Vod, SectionStatus.NotRequested, 0, null),
            new SectionResult(CatalogSection.Vod, SectionStatus.NotRequested, 0, null));
        Assert.AreEqual(
            new EpisodeRecord("10", 1, 2, "99", "Pilot", "mp4"),
            new EpisodeRecord("10", 1, 2, "99", "Pilot", "mp4"));
    }

    [TestMethod]
    public void ImportWarning_ExposesConstructorValues()
    {
        var samples = new[] { "a", "b" };
        var warning = new ImportWarning(CatalogSection.Live, ImportWarningCodes.M3uMissingUrl, 5, samples);

        Assert.AreEqual(CatalogSection.Live, warning.Section);
        Assert.AreEqual("M3U_MISSING_URL", warning.Code);
        Assert.AreEqual(5, warning.Count);
        Assert.AreSame(samples, warning.Samples);
    }

    [TestMethod]
    public void ImportWarningCodes_HaveExactValues()
    {
        // Read through reflection: asserting on compile-time constants directly triggers MSTEST0032.
        var actual = typeof(ImportWarningCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(static f => f.IsLiteral)
            .ToDictionary(static f => f.Name, static f => (string)f.GetRawConstantValue()!);

        var expected = new Dictionary<string, string>
        {
            ["M3uMissingUrl"] = "M3U_MISSING_URL",
            ["M3uEmptyTitle"] = "M3U_EMPTY_TITLE",
            ["M3uNoGroup"] = "M3U_NO_GROUP",
            ["M3uTypeDefaulted"] = "M3U_TYPE_DEFAULTED",
            ["XtreamInvalidItem"] = "XTREAM_INVALID_ITEM",
            ["XtreamEmptyTitle"] = "XTREAM_EMPTY_TITLE",
            ["XtreamMalformedDocument"] = "XTREAM_MALFORMED_DOCUMENT",
            ["XtreamUnknownCategory"] = "XTREAM_UNKNOWN_CATEGORY",
            ["XtreamAccountInfoUnavailable"] = "XTREAM_ACCOUNT_INFO_UNAVAILABLE",
            ["XmltvProgrammesDropped"] = "XMLTV_PROGRAMMES_DROPPED",
            ["XmltvChannelsDropped"] = "XMLTV_CHANNELS_DROPPED",
            ["XmltvDocumentTruncated"] = "XMLTV_DOCUMENT_TRUNCATED",
        };

        Assert.AreEqual(expected.Count, actual.Count);
        foreach (var (name, value) in expected)
        {
            Assert.IsTrue(actual.TryGetValue(name, out var actualValue), $"Missing constant {name}.");
            Assert.AreEqual(value, actualValue, name);
        }
    }

    [TestMethod]
    public void XtreamCredentials_ToString_NeverContainsSecrets()
    {
        var credentials = new XtreamCredentials("http://example.invalid:8080", "alice-user", "s3cret-pass");

        var text = credentials.ToString();

        Assert.AreEqual("XtreamCredentials { BaseUrl = http://example.invalid:8080, Username = ***, Password = *** }", text);
        Assert.IsFalse(text.Contains("alice-user", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("s3cret-pass", StringComparison.Ordinal));
        Assert.AreEqual("s3cret-pass", credentials.Password);
    }

    [TestMethod]
    public void ICatalogSink_HasExactlyFourMembers()
    {
        var names = typeof(ICatalogSink).GetMethods().Select(static m => m.Name).OrderBy(static n => n, StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(
            new[] { "AddGroupsAsync", "AddItemsAsync", "AddWarningsAsync", "SetSectionResultAsync" },
            names);
    }
}
