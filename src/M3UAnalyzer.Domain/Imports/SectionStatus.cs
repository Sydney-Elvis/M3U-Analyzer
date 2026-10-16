namespace M3UAnalyzer.Domain.Imports;

/// <summary>Outcome of one catalog section. Distinguishes "unknown" from "zero".</summary>
public enum SectionStatus
{
    /// <summary>Imported successfully. An item count of zero means the section is genuinely empty.</summary>
    Ok,
    /// <summary>The import was attempted and failed; the count is unknown.</summary>
    Failed,
    /// <summary>The source type cannot provide this section.</summary>
    NotSupported,
    /// <summary>The user did not request this section.</summary>
    NotRequested,
}
