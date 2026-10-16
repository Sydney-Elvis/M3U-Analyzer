using System.Globalization;
using System.Text.Json;

namespace M3UAnalyzer.Ingestion.Xtream;

public static class XtreamResponseParser
{
    private const long MaxUnixTimeSeconds = 253402300799;

    public static XtreamParseResult<XtreamCategory> ParseCategories(string json) =>
        ParseList(json, static element =>
        {
            var id = XtreamJson.ReadInt(element, "category_id");
            var name = XtreamJson.ReadString(element, "category_name")?.Trim();
            return id > 0 && !string.IsNullOrEmpty(name) ? new XtreamCategory(id, name) : null;
        });

    public static XtreamParseResult<XtreamLiveStream> ParseLiveStreams(string json) =>
        ParseList(json, static element =>
        {
            var id = XtreamJson.ReadInt(element, "stream_id");
            if (id <= 0)
            {
                return null;
            }

            return new XtreamLiveStream(
                id,
                XtreamJson.ReadString(element, "name") ?? string.Empty,
                XtreamJson.ReadOptionalText(element, "stream_icon"),
                XtreamJson.ReadOptionalText(element, "epg_channel_id"),
                XtreamJson.CategoryIds(element),
                XtreamJson.ReadNullableInt(element, "num"),
                XtreamJson.ReadFlag(element, "tv_archive"));
        });

    public static XtreamParseResult<XtreamVodStream> ParseVodStreams(string json) =>
        ParseList(json, static element =>
        {
            var id = XtreamJson.ReadInt(element, "stream_id");
            if (id <= 0)
            {
                return null;
            }

            return new XtreamVodStream(
                id,
                XtreamJson.ReadString(element, "name") ?? string.Empty,
                XtreamJson.ReadOptionalText(element, "stream_icon"),
                XtreamJson.CategoryIds(element),
                XtreamJson.ReadOptionalText(element, "container_extension"),
                XtreamJson.Year(element),
                XtreamJson.ReadOptionalText(element, "rating"),
                XtreamJson.Tmdb(element));
        });

    public static XtreamParseResult<XtreamSeriesSummary> ParseSeries(string json) =>
        ParseList(json, static element =>
        {
            var id = XtreamJson.ReadInt(element, "series_id");
            if (id <= 0)
            {
                return null;
            }

            return new XtreamSeriesSummary(
                id,
                XtreamJson.ReadString(element, "name") ?? string.Empty,
                XtreamJson.ReadOptionalText(element, "cover"),
                XtreamJson.CategoryIds(element),
                XtreamJson.ReadLong(element, "last_modified"),
                XtreamJson.Year(element),
                XtreamJson.Tmdb(element));
        });

    public static XtreamParseResult<XtreamEpisode> ParseSeriesInfo(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return new([], 0, true);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("episodes", out var episodes)
                || episodes.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            {
                return new([], 0, true);
            }

            var items = new List<XtreamEpisode>();
            var skipped = 0;
            if (episodes.ValueKind == JsonValueKind.Object)
            {
                foreach (var season in episodes.EnumerateObject())
                {
                    int? seasonFromKey = int.TryParse(season.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeason)
                        ? parsedSeason
                        : null;

                    if (season.Value.ValueKind != JsonValueKind.Array)
                    {
                        skipped++;
                        continue;
                    }

                    AddEpisodes(season.Value, seasonFromKey, items, ref skipped);
                }
            }
            else
            {
                foreach (var season in episodes.EnumerateArray())
                {
                    if (season.ValueKind != JsonValueKind.Array)
                    {
                        skipped++;
                        continue;
                    }

                    AddEpisodes(season, null, items, ref skipped);
                }
            }

            return new(items, skipped, false);
        }
    }

    public static XtreamAccountInfo? ParseAccountInfo(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("user_info", out var userInfo)
                || userInfo.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var exp = XtreamJson.ReadLong(userInfo, "exp_date");
            DateTimeOffset? expiresUtc = exp is > 0 and <= MaxUnixTimeSeconds ? DateTimeOffset.FromUnixTimeSeconds(exp) : null;
            var maxConnections = XtreamJson.ReadNullableInt(userInfo, "max_connections");
            if (maxConnections <= 0)
            {
                maxConnections = null;
            }

            return new XtreamAccountInfo(
                XtreamJson.ReadInt(userInfo, "auth") == 1,
                XtreamJson.ReadOptionalText(userInfo, "status"),
                expiresUtc,
                maxConnections);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static XtreamParseResult<T> ParseList<T>(string json, Func<JsonElement, T?> map)
        where T : class
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return new([], 0, true);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new([], 0, true);
            }

            var items = new List<T>();
            var skipped = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var item = element.ValueKind == JsonValueKind.Object ? map(element) : null;
                if (item is null)
                {
                    skipped++;
                }
                else
                {
                    items.Add(item);
                }
            }

            return new(items, skipped, false);
        }
    }

    private static void AddEpisodes(JsonElement episodeArray, int? seasonFromKey, List<XtreamEpisode> items, ref int skipped)
    {
        foreach (var episode in episodeArray.EnumerateArray())
        {
            if (episode.ValueKind != JsonValueKind.Object)
            {
                skipped++;
                continue;
            }

            var id = XtreamJson.ReadInt(episode, "id");
            if (id <= 0)
            {
                skipped++;
                continue;
            }

            items.Add(new XtreamEpisode(
                id,
                seasonFromKey ?? XtreamJson.ReadNullableInt(episode, "season"),
                XtreamJson.ReadNullableInt(episode, "episode_num"),
                XtreamJson.ReadString(episode, "title")?.Trim() ?? string.Empty,
                XtreamJson.ReadOptionalText(episode, "container_extension")));
        }
    }
}
