using System.IO;
using M3Undle.Core.M3u;
using M3UAnalyzer.Domain.Catalog;

namespace M3UAnalyzer.Ingestion.M3u;

/// <summary>Infers an M3U entry's content type from its URL and records how the decision was made.</summary>
public static class ContentTypeInference
{
    public static (ContentType Type, ContentTypeBasis Basis) FromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (ContentType.Unknown, ContentTypeBasis.Defaulted);
        }

        var core = LiveClassifier.ClassifyContent(url);

        if (core == "vod")
        {
            return (ContentType.Vod, ContentTypeBasis.UrlInferred);
        }

        if (core == "series")
        {
            return (ContentType.Series, ContentTypeBasis.UrlInferred);
        }

        // Otherwise Live. Determine whether there is positive live evidence.
        var evidence = HasLiveEvidence(url);
        return (ContentType.Live, evidence ? ContentTypeBasis.UrlInferred : ContentTypeBasis.Defaulted);
    }

    private static bool HasLiveEvidence(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // (a) path segment equals "live"
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments)
            {
                if (string.Equals(seg, "live", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // (b) query pair type/kind = live
            var query = uri.Query;
            if (!string.IsNullOrEmpty(query))
            {
                var q = query.TrimStart('?');
                foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = pair.IndexOf('=');
                    string rawKey, rawValue;
                    if (eq < 0)
                    {
                        rawKey = pair;
                        rawValue = string.Empty;
                    }
                    else
                    {
                        rawKey = pair.Substring(0, eq);
                        rawValue = pair.Substring(eq + 1);
                    }

                    var key = Uri.UnescapeDataString(rawKey);
                    var value = Uri.UnescapeDataString(rawValue);
                    if ((string.Equals(key, "type", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(key, "kind", StringComparison.OrdinalIgnoreCase)) &&
                        string.Equals(value, "live", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // (c) extension
            var ext = Path.GetExtension(uri.AbsolutePath);
            if (string.Equals(ext, ".ts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".m3u8", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".m2ts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".mts", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        // not absolute URI
        return url.Contains("/live/", StringComparison.OrdinalIgnoreCase);
    }
}
