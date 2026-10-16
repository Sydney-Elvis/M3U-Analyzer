namespace M3UAnalyzer.Domain.Epg;

/// <summary>Per-channel guide facts for one snapshot. <see cref="IsDeclared"/> is false for programmes whose channel has no &lt;channel&gt; element.</summary>
public sealed record EpgChannelSummary(string XmltvChannelId, string DisplayName, bool IsDeclared, int ProgrammeCount, DateTimeOffset? FirstStartUtc, DateTimeOffset? LastStopUtc);
