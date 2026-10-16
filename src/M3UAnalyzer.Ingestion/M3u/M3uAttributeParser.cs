using System.Text.RegularExpressions;

namespace M3UAnalyzer.Ingestion.M3u;

/// <summary>Extracts raw <c>key="value"</c> attributes from an M3U entry, plus the <c>#EXTGRP</c> group.</summary>
public static class M3uAttributeParser
{
    /// <summary>Key under which the <c>#EXTGRP:</c> value is stored.</summary>
    public const string ExtGrpKey = "extgrp";

    private static readonly Regex AttributeRegex = new(
        @"(?<key>[A-Za-z0-9_\-]+)=""(?<value>[^""]*)""",
        RegexOptions.Compiled);

    public static IReadOnlyDictionary<string, string> Parse(IReadOnlyList<string> metadataLines)
    {
        ArgumentNullException.ThrowIfNull(metadataLines);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (metadataLines.Count == 0)
        {
            return result;
        }

        foreach (Match match in AttributeRegex.Matches(metadataLines[0]))
        {
            var key = match.Groups["key"].Value.ToLowerInvariant();
            var value = match.Groups["value"].Value;
            result.TryAdd(key, value);
        }

        for (var i = 1; i < metadataLines.Count; i++)
        {
            var line = metadataLines[i];
            if (line is null)
            {
                continue;
            }

            const string prefix = "#EXTGRP:";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = line.Substring(prefix.Length).Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    result.TryAdd(ExtGrpKey, value);
                }
            }
        }

        return result;
    }
}
