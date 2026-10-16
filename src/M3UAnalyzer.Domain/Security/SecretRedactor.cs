using System.Text.RegularExpressions;

namespace M3UAnalyzer.Domain.Security;

/// <summary>Removes credentials and signed tokens from URL-like strings before they are logged, stored, or shown.</summary>
public static class SecretRedactor
{
    /// <summary>Replacement text for every removed secret.</summary>
    public const string Mask = "***";

    private static readonly Regex SchemeRegex = new(@"^[A-Za-z][A-Za-z0-9+.\-]*://", RegexOptions.Compiled);
    private static readonly Regex NumericLastSegmentRegex = new(@"^\d+(\.[A-Za-z0-9]+)?$", RegexOptions.Compiled);

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "live", "movie", "series", "timeshift",
    };

    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "username", "password", "user", "pass", "pwd", "token", "access_token",
        "auth", "key", "apikey", "api_key", "signature", "sig",
    };

    /// <summary>Returns <paramref name="value"/> with secrets masked. Null stays null; non-URL text is returned unchanged.</summary>
    public static string? Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var schemeMatch = SchemeRegex.Match(value);
        if (!schemeMatch.Success)
        {
            return value;
        }

        var scheme = schemeMatch.Value;
        var rest = value.Substring(scheme.Length);

        // authority
        int authEnd = rest.Length;
        for (int i = 0; i < rest.Length; i++)
        {
            char c = rest[i];
            if (c == '/' || c == '?' || c == '#')
            {
                authEnd = i;
                break;
            }
        }
        var authority = rest.Substring(0, authEnd);
        var tail = rest.Substring(authEnd);

        // user info
        int lastAt = authority.LastIndexOf('@');
        if (lastAt >= 0)
        {
            authority = Mask + authority.Substring(lastAt);
        }

        // fragment removal
        int hashIdx = tail.IndexOf('#');
        if (hashIdx >= 0)
        {
            tail = tail.Substring(0, hashIdx);
        }

        // split path/query
        string path;
        string? query = null;
        int qIdx = tail.IndexOf('?');
        if (qIdx >= 0)
        {
            path = tail.Substring(0, qIdx);
            query = tail.Substring(qIdx + 1);
        }
        else
        {
            path = tail;
        }

        // Path rule A
        bool ruleAApplied = false;
        var segments = path.Split('/');
        // find non-empty segment indices
        var nonEmptyIndices = new List<int>();
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length > 0)
            {
                nonEmptyIndices.Add(i);
            }
        }

        // walk non-empty segments in order
        for (int k = 0; k < nonEmptyIndices.Count; k++)
        {
            int segIdx = nonEmptyIndices[k];
            if (Keywords.Contains(segments[segIdx]) && k + 2 < nonEmptyIndices.Count)
            {
                // replace next two non-empty segments
                int i1 = nonEmptyIndices[k + 1];
                int i2 = nonEmptyIndices[k + 2];
                segments[i1] = Mask;
                segments[i2] = Mask;
                ruleAApplied = true;
                break;
            }
        }

        if (!ruleAApplied)
        {
            // Path rule B
            if (nonEmptyIndices.Count >= 3)
            {
                int lastIdx = nonEmptyIndices[nonEmptyIndices.Count - 1];
                if (NumericLastSegmentRegex.IsMatch(segments[lastIdx]))
                {
                    int i1 = nonEmptyIndices[nonEmptyIndices.Count - 2];
                    int i2 = nonEmptyIndices[nonEmptyIndices.Count - 3];
                    segments[i1] = Mask;
                    segments[i2] = Mask;
                }
            }
        }

        path = string.Join("/", segments);

        // Query rule
        if (query is not null)
        {
            var parts = query.Split('&');
            for (int i = 0; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq >= 0)
                {
                    var key = parts[i].Substring(0, eq);
                    if (SensitiveQueryKeys.Contains(key))
                    {
                        parts[i] = key + "=" + Mask;
                    }
                }
            }
            query = string.Join("&", parts);
        }

        var result = scheme + authority + path;
        if (query is not null)
        {
            result += "?" + query;
        }
        return result;
    }
}
