using M3UAnalyzer.Domain.Epg;
using M3Undle.Core.Epg;

namespace M3UAnalyzer.Ingestion.Epg;

public static class EpgSummaryBuilder
{
    public static IReadOnlyList<EpgChannelSummary> Build(EpgCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        var declared = new Dictionary<string, EpgChannelRecord>(StringComparer.Ordinal);
        foreach (var channel in catalogue.Channels)
        {
            declared.TryAdd(channel.XmltvChannelId, channel);
        }

        var channelIds = declared.Keys
            .Concat(catalogue.ProgrammesByChannel.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal);

        var summaries = new List<EpgChannelSummary>();
        foreach (var channelId in channelIds)
        {
            var isDeclared = false;
            var displayName = channelId;
            if (declared.TryGetValue(channelId, out var channel))
            {
                isDeclared = true;
                displayName = channel.DisplayName;
            }

            catalogue.ProgrammesByChannel.TryGetValue(channelId, out var programmes);
            programmes ??= Array.Empty<EpgProgrammeRecord>();

            DateTimeOffset? firstStart = programmes.Count == 0
                ? null
                : programmes.Min(static programme => programme.StartUtc);
            DateTimeOffset? lastStop = programmes.Count == 0
                ? null
                : programmes.Max(static programme => programme.StopUtc);

            summaries.Add(new EpgChannelSummary(
                channelId,
                displayName,
                isDeclared,
                programmes.Count,
                firstStart,
                lastStop));
        }

        return summaries;
    }
}
