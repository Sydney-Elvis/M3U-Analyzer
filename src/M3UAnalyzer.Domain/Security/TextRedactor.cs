using System.Text.RegularExpressions;

namespace M3UAnalyzer.Domain.Security;

/// <summary>Redacts every URL embedded in free text (error messages, log lines, warning samples).</summary>
public static class TextRedactor
{
    // A URL ends at whitespace, a quote or an angle bracket, and its last character is never one of
    // . , ; : ! ? ) ] }. That punctuation stays in the text after the URL, so it cannot hide the
    // numeric last segment that SecretRedactor's Xtream short-form rule needs.
    // NonBacktracking keeps matching linear in the text length (the text may come from a provider).
    private static readonly Regex UrlRegex = new(
        "[A-Za-z][A-Za-z0-9+.\\-]*://[^\\s\"'<>]*[^\\s\"'<>.,;:!?)\\]}]",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>Returns <paramref name="text"/> with each embedded URL passed through <see cref="SecretRedactor.Redact"/>.</summary>
    public static string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return UrlRegex.Replace(text, static match => SecretRedactor.Redact(match.Value)!);
    }
}