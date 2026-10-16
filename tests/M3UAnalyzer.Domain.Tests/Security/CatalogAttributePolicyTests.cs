using System.Text.Json;
using M3UAnalyzer.Domain.Catalog;
using M3UAnalyzer.Domain.Security;

namespace M3UAnalyzer.Domain.Tests.Security;

[TestClass]
public sealed class CatalogAttributePolicyTests
{
    [TestMethod]
    [DataRow("tvg-id", "cnn.us")]
    [DataRow("tvg-id", "Sky Sports Main Event HD.uk")]
    [DataRow("tvg-chno", "101")]
    [DataRow("tvg-country", "CA")]
    [DataRow("tvg-language", "English;French")]
    [DataRow("tvg-shift", "-2.5")]
    [DataRow("catchup", "default")]
    [DataRow("catchup-days", "7")]
    [DataRow("catchup-type", "flussonic")]
    [DataRow("rating", "PG-13")]
    [DataRow("rating", "8.7")]
    [DataRow("tmdb", "603")]
    [DataRow("num", "1")]
    [DataRow("tv_archive", "0")]
    [DataRow("last_modified", "1760000000")]
    [DataRow("tvg-country", "")]
    [DataRow("tvg-name", "ESPN, Inc")]
    [DataRow("tvg-name", "AT&T SportsNet ?")]
    [DataRow("tvg-name", "News/Weather @ 6")]
    [DataRow("group-title", "Movies | Action")]
    [DataRow("extgrp", "Sports")]
    public void IsPersistable_AllowlistedKeyWithSafeValue_IsTrue(string key, string value)
    {
        Assert.IsTrue(CatalogAttributePolicy.IsPersistable(key, value), $"{key}={value}");
    }

    [TestMethod]
    [DataRow("catchup-source", "http://example.invalid/timeshift/alice/s3cret/{duration}/{start}/1.ts")]
    [DataRow("catchup-source", "?utc={utc}&lutc={lutc}")]
    [DataRow("url-tvg", "http://example.invalid/xmltv.php?username=alice&password=s3cret")]
    [DataRow("tvg-url", "http://example.invalid/epg.xml")]
    [DataRow("tvg-logo", "http://example.invalid/logo.png?token=s3cret")]
    [DataRow("tvg-logo-small", "https://cdn.example.invalid/x.png?X-Amz-Signature=s3cret")]
    [DataRow("x-auth", "s3cret")]
    [DataRow("parent-code", "1234")]
    [DataRow("tvg-id", "http://example.invalid/live/alice/s3cret/1.ts")]
    [DataRow("tvg-id", "alice/s3cret/1001")]
    [DataRow("tvg-id", "alice:s3cret@example.invalid")]
    [DataRow("rating", "8.7?sig=s3cret")]
    [DataRow("tvg-chno", "0123456789012345678901234567890123456789012345678901234567890123456789")]
    [DataRow("tvg-name", "Mirror http://example.invalid:8080/alice/s3cret/12345")]
    [DataRow("group-title", "rtmp://example.invalid/app/s3cret")]
    [DataRow("tvg-name", "line\nbreak")]
    public void IsPersistable_UnknownKeyOrUnsafeValue_IsFalse(string key, string value)
    {
        Assert.IsFalse(CatalogAttributePolicy.IsPersistable(key, value), $"{key}={value}");
    }

    [TestMethod]
    public void IsPersistable_TextLongerThanLimit_IsFalse()
    {
        Assert.IsTrue(CatalogAttributePolicy.IsPersistable("tvg-name", new string('a', CatalogAttributePolicy.MaxTextValueLength)));
        Assert.IsFalse(CatalogAttributePolicy.IsPersistable("tvg-name", new string('a', CatalogAttributePolicy.MaxTextValueLength + 1)));
    }

    [TestMethod]
    public void Filter_KeepsSafeValues_AndWithholdsTheRestByKeyNameOnly()
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tvg-id"] = "cnn.us",
            ["tvg-logo"] = "http://example.invalid/logo.png?token=s3cret",
            ["catchup-source"] = "http://example.invalid/timeshift/alice/s3cret/{duration}/{start}/1.ts",
            ["x-custom"] = "s3cret",
            ["tvg-country"] = "US",
            ["rating"] = "http://example.invalid/r?sig=s3cret",
        };

        var result = CatalogAttributePolicy.Filter(raw);

        CollectionAssert.AreEquivalent(new[] { "tvg-id", "tvg-country" }, result.Kept.Keys.ToArray());
        Assert.AreEqual("cnn.us", result.Kept["tvg-id"]);
        CollectionAssert.AreEqual(new[] { "catchup-source", "rating", "x-custom" }, result.WithheldKeys.ToArray(), "sorted ordinal; a usable tvg-logo moves to LogoUrl and is not withheld");
        var json = JsonSerializer.Serialize(result);
        Assert.IsFalse(json.Contains("s3cret", StringComparison.Ordinal), "no withheld value may survive");
        Assert.IsFalse(json.Contains("alice", StringComparison.Ordinal));
        Assert.AreEqual(6, raw.Count, "the input is not modified");
    }

    [TestMethod]
    [DataRow("data:image/png;base64,AAAA")]
    [DataRow("ftp://example.invalid/logo.png")]
    [DataRow("logo.png")]
    public void Filter_UnusableLogo_IsWithheld(string logo)
    {
        var result = CatalogAttributePolicy.Filter(new Dictionary<string, string> { [CatalogAttributePolicy.LogoKey] = logo });

        Assert.AreEqual(0, result.Kept.Count);
        CollectionAssert.AreEqual(new[] { "tvg-logo" }, result.WithheldKeys.ToArray());
    }

    [TestMethod]
    [DataRow("http://example.invalid/logos/cnn.png", "http://example.invalid/logos/cnn.png")]
    [DataRow("  http://example.invalid/a.png#frag  ", "http://example.invalid/a.png")]
    [DataRow("http://example.invalid/img/x.png?token=s3cret", "http://example.invalid/img/x.png")]
    [DataRow("https://cdn.example.invalid/x.png?X-Amz-Signature=s3cret&X-Amz-Credential=alice", "https://cdn.example.invalid/x.png")]
    [DataRow("https://cdn.example.invalid/x.png?hdnts=exp=1~hmac=s3cret", "https://cdn.example.invalid/x.png")]
    [DataRow("http://alice:s3cret@example.invalid/x.png", "http://example.invalid/x.png")]
    [DataRow("http://example.invalid:8080/images/alice/s3cret/1001.png", "http://example.invalid:8080/images/***/***/1001.png")]
    [DataRow("http://example.invalid/live/alice/s3cret/1.png", "http://example.invalid/live/***/***/1.png")]
    [DataRow("http://example.invalid:80/my logo.png", "http://example.invalid/my%20logo.png")]
    [DataRow("data:image/png;base64,AAAA", null)]
    [DataRow("ftp://example.invalid/logo.png", null)]
    [DataRow("logo.png", null)]
    [DataRow("   ", null)]
    [DataRow(null, null)]
    public void SanitizeLogoUrl_KeepsOnlySchemeHostAndRedactedPath(string? input, string? expected)
    {
        var once = CatalogAttributePolicy.SanitizeLogoUrl(input);
        var twice = CatalogAttributePolicy.SanitizeLogoUrl(once);

        Assert.AreEqual(expected, once);
        Assert.AreEqual(once, twice, "sanitizing is idempotent");
    }

    [TestMethod]
    public void SanitizeLogoUrl_OverLongResult_IsNull()
    {
        Assert.IsNull(CatalogAttributePolicy.SanitizeLogoUrl("http://example.invalid/" + new string('a', CatalogAttributePolicy.MaxLogoUrlLength)));
    }

    [TestMethod]
    public void Filter_EmptyInput_KeepsAndWithholdsNothing()
    {
        var result = CatalogAttributePolicy.Filter(new Dictionary<string, string>());

        Assert.AreEqual(0, result.Kept.Count);
        Assert.AreEqual(0, result.WithheldKeys.Count);
    }

    [TestMethod]
    public void Filter_InvalidOrLongKeyNames_AreReportedWithAPlaceholder()
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [new string('k', CatalogAttributePolicy.MaxKeyLength + 1)] = "1",
            ["Bad Key"] = "2",
            ["also-unknown"] = "3",
        };

        var result = CatalogAttributePolicy.Filter(raw);

        CollectionAssert.AreEqual(new[] { CatalogAttributePolicy.InvalidKeyPlaceholder, "also-unknown" }, result.WithheldKeys.ToArray());
    }

    [TestMethod]
    public void EnsurePersistable_AcceptsAFilteredItem()
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tvg-country"] = "CA",
            ["catchup-source"] = "http://example.invalid/timeshift/alice/s3cret/{duration}/{start}/1.ts",
        };
        var filtered = CatalogAttributePolicy.Filter(raw);
        var item = Item(3) with
        {
            Attributes = filtered.Kept,
            WithheldAttributeKeys = filtered.WithheldKeys,
            LogoUrl = CatalogAttributePolicy.SanitizeLogoUrl("http://example.invalid/logo.png?token=s3cret"),
        };

        CatalogAttributePolicy.EnsurePersistable(item);
    }

    [TestMethod]
    public void EnsurePersistable_UnfilteredCredentialAttribute_Throws_WithoutTheValue()
    {
        var item = Item(42) with
        {
            Attributes = new Dictionary<string, string> { ["catchup-source"] = "http://example.invalid/timeshift/alice/s3cret/{duration}/{start}/1.ts" },
        };

        var ex = Assert.ThrowsExactly<UnsafeCatalogDataException>(() => CatalogAttributePolicy.EnsurePersistable(item));

        Assert.AreEqual(42, ex.Ordinal);
        Assert.AreEqual("Catalog item 42: attribute 'catchup-source' failed the persistence safety check.", ex.Message);
    }

    [TestMethod]
    public void EnsurePersistable_AllowlistedKeyWithUrlValue_Throws()
    {
        var item = Item(1) with { Attributes = new Dictionary<string, string> { ["tvg-id"] = "http://example.invalid/live/alice/s3cret/1.ts" } };

        var ex = Assert.ThrowsExactly<UnsafeCatalogDataException>(() => CatalogAttributePolicy.EnsurePersistable(item));

        Assert.IsFalse(ex.Message.Contains("s3cret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EnsurePersistable_UnredactedLogoUrl_Throws()
    {
        var item = Item(2) with { LogoUrl = "http://example.invalid/images/alice/s3cret/1001.png" };

        var ex = Assert.ThrowsExactly<UnsafeCatalogDataException>(() => CatalogAttributePolicy.EnsurePersistable(item));

        Assert.AreEqual("Catalog item 2: logo URL failed the persistence safety check.", ex.Message);
    }

    [TestMethod]
    public void EnsurePersistable_SignedLogoUrl_Throws()
    {
        var item = Item(4) with { LogoUrl = "https://cdn.example.invalid/x.png?X-Amz-Signature=s3cret" };

        Assert.ThrowsExactly<UnsafeCatalogDataException>(() => CatalogAttributePolicy.EnsurePersistable(item));
    }

    [TestMethod]
    public void EnsurePersistable_InvalidWithheldKeyName_Throws()
    {
        var item = Item(5) with { WithheldAttributeKeys = ["s3cret value"] };

        var ex = Assert.ThrowsExactly<UnsafeCatalogDataException>(() => CatalogAttributePolicy.EnsurePersistable(item));

        Assert.AreEqual("Catalog item 5: withheld attribute key failed the persistence safety check.", ex.Message);
    }

    [TestMethod]
    public void RawCatalogItem_WithheldAttributeKeys_DefaultsToEmpty()
    {
        Assert.AreEqual(0, Item(0).WithheldAttributeKeys.Count);
    }

    private static RawCatalogItem Item(int ordinal) => new()
    {
        Ordinal = ordinal,
        ContentType = ContentType.Live,
        ContentTypeBasis = ContentTypeBasis.UrlInferred,
        RawTitle = "Channel",
        DisplayTitle = "Channel",
    };
}
