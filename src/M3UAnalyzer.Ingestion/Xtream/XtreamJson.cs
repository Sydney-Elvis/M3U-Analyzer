using System.Globalization;
using System.Text.Json;

namespace M3UAnalyzer.Ingestion.Xtream;

public static class XtreamJson
{
    public static int? Year(JsonElement obj)
    {
        var year = ReadNullableInt(obj, "year");
        return year is >= 1888 and <= 2100 ? year : null;
    }

    public static string? Tmdb(JsonElement obj)
    {
        var tmdb = ReadOptionalText(obj, "tmdb") ?? ReadOptionalText(obj, "tmdb_id");
        return tmdb == "0" ? null : tmdb;
    }

    public static int ReadInt(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.TryGetInt32(out var number) ? number : 0;
        }

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString()?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    public static int ReadInt(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) ? ReadInt(value) : 0;

    public static int? ReadNullableInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.TryGetInt32(out var number) ? number : null;
        }

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString()?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public static long ReadLong(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.TryGetInt64(out var number) ? number : 0;
        }

        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString()?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    public static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static string? ReadOptionalText(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };

        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static bool? ReadFlag(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => value.GetString()?.Trim(),
            _ => null,
        };

        return text switch
        {
            "1" => true,
            "0" => false,
            _ => null,
        };
    }

    public static IReadOnlyList<int> CategoryIds(JsonElement obj)
    {
        if (obj.TryGetProperty("category_ids", out var values) && values.ValueKind == JsonValueKind.Array)
        {
            var ids = values.EnumerateArray()
                .Select(ReadInt)
                .Where(static id => id > 0)
                .Distinct()
                .ToArray();
            if (ids.Length > 0)
            {
                return ids;
            }
        }

        var categoryId = ReadInt(obj, "category_id");
        return categoryId > 0 ? [categoryId] : [];
    }
}
