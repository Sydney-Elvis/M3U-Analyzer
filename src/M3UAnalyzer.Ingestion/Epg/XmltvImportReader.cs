using System.Xml;
using M3UAnalyzer.Domain.Epg;
using M3Undle.Core.Epg;

namespace M3UAnalyzer.Ingestion.Epg;

public sealed record XmltvReadResult(EpgCatalogue Catalogue, EpgParseStats Stats);

public static class XmltvImportReader
{
    public static XmltvReadResult Read(string sourceId, string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentNullException.ThrowIfNull(xml);

        var catalogue = new XmltvParser().Parse(sourceId, xml);
        var parsedProgrammes = catalogue.ProgrammesByChannel.Values.Sum(static programmes => programmes.Count);

        if (string.IsNullOrWhiteSpace(xml))
        {
            return new(catalogue, new EpgParseStats(0, 0, 0, 0, false));
        }

        var rawChannels = 0;
        var rawProgrammes = 0;
        var truncated = false;
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        try
        {
            using var textReader = new StringReader(xml);
            using var reader = XmlReader.Create(textReader, settings);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.Depth != 1)
                {
                    continue;
                }

                if (reader.LocalName == "channel")
                {
                    rawChannels++;
                }
                else if (reader.LocalName == "programme")
                {
                    rawProgrammes++;
                }
            }
        }
        catch (XmlException)
        {
            truncated = true;
        }

        var stats = new EpgParseStats(
            rawChannels,
            catalogue.Channels.Count,
            rawProgrammes,
            parsedProgrammes,
            truncated);
        return new(catalogue, stats);
    }
}
