using System.Globalization;

namespace M3UAnalyzer.TestSupport;

public sealed record SyntheticCatalogSpec(int Live, int Vod, int SeriesEpisodes, int Seed = 42);

public sealed record SyntheticCatalogExpectations(int Entries, int Live, int Vod, int Series, int Unknown, int ExactDuplicates);

public static class SyntheticCatalog
{
    public const string Host = "http://gen.example.invalid:8080";
    public const string Username = "gen-user";
    public const string Password = "gen-secret-pw";

    private static readonly string[] Words = new[]
    {
        "Alpha", "Bravo", "Cosmo", "Delta", "Echo", "Fusion", "Galaxy", "Harbor", "Indigo", "Jupiter",
        "Kinetic", "Lumen", "Metro", "Nova", "Orbit", "Prime", "Quest", "Radiant", "Summit", "Titan"
    };

    private static readonly string[] Suffixes = new[] { "", " HD", " FHD", " 4K" };

    public static bool IsDuplicate(int liveIndex) => liveIndex % 20 == 19;

    public static bool IsMissingUrl(int liveIndex) => !IsDuplicate(liveIndex) && liveIndex % 50 == 49;

    public static string GenerateM3u(SyntheticCatalogSpec spec)
    {
        if (spec.Live < 0 || spec.Vod < 0 || spec.SeriesEpisodes < 0)
            throw new ArgumentOutOfRangeException(nameof(spec));

        var rng = new Random(spec.Seed);
        var sb = new System.Text.StringBuilder();
        sb.Append("#EXTM3U\n");

        // Live entries
        string? previousExtinf = null;
        string? previousUrl = null;
        for (int i = 0; i < spec.Live; i++)
        {
            if (IsDuplicate(i) && i > 0 && previousExtinf is not null)
            {
                sb.Append(previousExtinf);
                sb.Append('\n');
                if (previousUrl is not null)
                {
                    sb.Append(previousUrl);
                    sb.Append('\n');
                }
                continue;
            }

            string word1 = Words[rng.Next(Words.Length)];
            string word2 = Words[rng.Next(Words.Length)];
            string name = $"{word1} {word2} {i}{Suffixes[i % 4]}";
            string group = $"Live Group {(i / 7) % 40:D2}";

            var extinf = new System.Text.StringBuilder();
            extinf.Append("#EXTINF:-1");
            if (i % 10 < 7)
                extinf.Append($" tvg-id=\"ch{i}.gen\"");
            extinf.Append($" tvg-name=\"{name}\" group-title=\"{group}\"");
            extinf.Append($",{name}");

            string extinfLine = extinf.ToString();
            sb.Append(extinfLine);
            sb.Append('\n');

            if (!IsMissingUrl(i))
            {
                string url = $"{Host}/live/{Username}/{Password}/{100000 + i}.ts";
                sb.Append(url);
                sb.Append('\n');
            }

            previousExtinf = extinfLine;
            previousUrl = IsMissingUrl(i) ? null : $"{Host}/live/{Username}/{Password}/{100000 + i}.ts";
        }

        // VOD entries
        for (int j = 0; j < spec.Vod; j++)
        {
            string word = Words[rng.Next(Words.Length)];
            string line = $"#EXTINF:-1 group-title=\"Movies {j % 20:D2}\",{word} Movie {j} ({1950 + j % 70})";
            sb.Append(line);
            sb.Append('\n');
            string url = $"{Host}/movie/{Username}/{Password}/{200000 + j}.mkv";
            sb.Append(url);
            sb.Append('\n');
        }

        // Series episodes
        for (int k = 0; k < spec.SeriesEpisodes; k++)
        {
            int show = k / 10;
            int season = (k % 10) / 5 + 1;
            int episode = k % 5 + 1;
            string line = $"#EXTINF:-1 group-title=\"Series {show % 10}\",Show {show:D4} S{season:D2} E{episode:D2}";
            sb.Append(line);
            sb.Append('\n');
            string url = $"{Host}/series/{Username}/{Password}/{300000 + k}.mkv";
            sb.Append(url);
            sb.Append('\n');
        }

        return sb.ToString();
    }

    public static SyntheticCatalogExpectations Expect(SyntheticCatalogSpec spec)
    {
        if (spec.Live < 0 || spec.Vod < 0 || spec.SeriesEpisodes < 0)
            throw new ArgumentOutOfRangeException(nameof(spec));

        int liveCount = 0;
        int unknownCount = 0;
        int exactDuplicates = 0;

        // simulate live loop
        bool? previousHadUrl = null;
        for (int i = 0; i < spec.Live; i++)
        {
            if (IsDuplicate(i) && i > 0)
            {
                exactDuplicates++;
                if (previousHadUrl == true)
                    liveCount++;
                else
                    unknownCount++;
                continue;
            }

            bool missing = IsMissingUrl(i);
            if (missing)
                unknownCount++;
            else
                liveCount++;

            previousHadUrl = !missing;
        }

        return new SyntheticCatalogExpectations(
            Entries: spec.Live + spec.Vod + spec.SeriesEpisodes,
            Live: liveCount,
            Vod: spec.Vod,
            Series: spec.SeriesEpisodes,
            Unknown: unknownCount,
            ExactDuplicates: exactDuplicates);
    }
}
