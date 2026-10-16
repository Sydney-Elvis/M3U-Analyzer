using M3UAnalyzer.Domain.Security;

namespace M3UAnalyzer.Domain.Imports;

/// <summary>Aggregates import warnings per section and code, keeping a few redacted samples.</summary>
public sealed class ImportWarningCollector
{
    public const int MaxSamplesPerCode = 3;
    public const int MaxSampleLength = 120;

    private readonly Dictionary<(CatalogSection Section, string Code), (int Count, List<string> Samples)> _entries = new();
    private int _totalCount;

    /// <summary>Total number of <see cref="Add"/> calls.</summary>
    public int TotalCount => _totalCount;

    /// <summary>Counts one warning. Throws <see cref="ArgumentException"/> when <paramref name="code"/> is empty or whitespace.</summary>
    public void Add(CatalogSection section, string code, string? sample = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        _totalCount++;

        var key = (section, code);
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = (0, new List<string>());
        }
        entry.Count++;

        if (!string.IsNullOrWhiteSpace(sample))
        {
            var redacted = TextRedactor.Redact(sample!.Trim())!;
            if (redacted.Length > MaxSampleLength)
            {
                redacted = redacted.Substring(0, MaxSampleLength - 1) + "…";
            }

            if (entry.Samples.Count < MaxSamplesPerCode && !entry.Samples.Contains(redacted))
            {
                entry.Samples.Add(redacted);
            }
        }

        _entries[key] = entry;
    }

    /// <summary>One <see cref="ImportWarning"/> per (section, code), ordered by section (enum order) then code (ordinal).</summary>
    public IReadOnlyList<ImportWarning> ToWarnings()
    {
        return _entries
            .OrderBy(e => e.Key.Section)
            .ThenBy(e => e.Key.Code, StringComparer.Ordinal)
            .Select(e => new ImportWarning(e.Key.Section, e.Key.Code, e.Value.Count, e.Value.Samples))
            .ToList();
    }
}