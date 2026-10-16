namespace M3UAnalyzer.Domain.Epg;

/// <summary>Raw element counts versus what the parser kept. The difference is data the guide contained but the parser dropped.</summary>
public sealed record EpgParseStats(int RawChannelElements, int ParsedChannels, int RawProgrammeElements, int ParsedProgrammes, bool DocumentTruncated)
{
    public int DroppedChannels => Math.Max(0, RawChannelElements - ParsedChannels);

    public int DroppedProgrammes => Math.Max(0, RawProgrammeElements - ParsedProgrammes);
}
