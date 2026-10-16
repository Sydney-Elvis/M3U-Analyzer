using M3UAnalyzer.Domain.Catalog;
using System.Text.RegularExpressions;

namespace M3UAnalyzer.Domain.Security;

public static class CatalogAttributePolicy
{
    public const int MaxTextValueLength = 256;
    public const int MaxTokenValueLength = 64;
    public const int MaxKeyLength = 64;
    public const int MaxLogoUrlLength = 2048;
    public const string InvalidKeyPlaceholder = "(invalid key)";
    public const string LogoKey = "tvg-logo";

    private static readonly HashSet<string> TextKeysSet = new(StringComparer.Ordinal)
    {
        "tvg-name", "group-title", "extgrp",
    };
    private static readonly HashSet<string> TokenKeysSet = new(StringComparer.Ordinal)
    {
        "tvg-id", "tvg-chno", "tvg-country", "tvg-language", "tvg-shift", "tvg-type",
        "catchup", "catchup-days", "catchup-type", "timeshift", "radio",
        "num", "tv_archive", "rating", "tmdb", "last_modified",
    };

    public static IReadOnlySet<string> TextKeys { get; } = TextKeysSet;
    public static IReadOnlySet<string> TokenKeys { get; } = TokenKeysSet;

    private static readonly Regex TokenValueRegex = new(@"^[\p{L}\p{N} ._+\-|,;:()#']*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex KeyNameRegex = new(@"^[a-z0-9_-]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static bool IsPersistable(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (TokenKeysSet.Contains(key))
        {
            return value.Length <= MaxTokenValueLength && TokenValueRegex.IsMatch(value);
        }
        if (TextKeysSet.Contains(key))
        {
            return value.Length <= MaxTextValueLength
                && !value.Contains("://", StringComparison.Ordinal)
                && !value.Any(char.IsControl);
        }
        return false;
    }

    public static CatalogAttributeFilterResult Filter(IReadOnlyDictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        var withheld = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, value) in attributes)
        {
            if (key == LogoKey)
            {
                if (!string.IsNullOrWhiteSpace(value) && SanitizeLogoUrl(value) is null)
                {
                    withheld.Add(key);
                }
                continue;
            }

            if (IsPersistable(key, value))
            {
                kept[key] = value;
                continue;
            }

            // withhold
            string recordedKey;
            if (key.Length > MaxKeyLength || !KeyNameRegex.IsMatch(key))
            {
                recordedKey = InvalidKeyPlaceholder;
            }
            else
            {
                recordedKey = key;
            }
            withheld.Add(recordedKey);
        }

        var withheldSorted = withheld.OrderBy(k => k, StringComparer.Ordinal).ToList();
        return new CatalogAttributeFilterResult(kept, withheldSorted);
    }

    public static string? SanitizeLogoUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }
        if (string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        var reduced = SecretRedactor.Redact(uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped));
        if (reduced is null)
        {
            return null;
        }
        if (reduced.Length > MaxLogoUrlLength)
        {
            return null;
        }
        return reduced;
    }

    public static void EnsurePersistable(RawCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        foreach (var (key, value) in item.Attributes)
        {
            string fieldKey;
            if (key.Length > MaxKeyLength || !KeyNameRegex.IsMatch(key))
            {
                fieldKey = InvalidKeyPlaceholder;
            }
            else
            {
                fieldKey = key;
            }

            if (!IsPersistable(key, value))
            {
                throw new UnsafeCatalogDataException(item.Ordinal, $"attribute '{fieldKey}'");
            }
        }

        foreach (var withheld in item.WithheldAttributeKeys)
        {
            if (withheld == InvalidKeyPlaceholder)
            {
                continue;
            }
            if (withheld.Length > MaxKeyLength || !KeyNameRegex.IsMatch(withheld))
            {
                throw new UnsafeCatalogDataException(item.Ordinal, "withheld attribute key");
            }
        }

        if (item.LogoUrl is not null)
        {
            if (item.LogoUrl != SanitizeLogoUrl(item.LogoUrl))
            {
                throw new UnsafeCatalogDataException(item.Ordinal, "logo URL");
            }
        }
    }
}
