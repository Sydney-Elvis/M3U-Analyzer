namespace M3UAnalyzer.Ingestion.Fetching;

/// <summary>Limits applied to one fetch.</summary>
public sealed record FetchPolicy(long MaxBytes, TimeSpan IdleTimeout, TimeSpan HardTimeout, int MaxRedirects, bool AllowPrivateNetworks)
{
    public static FetchPolicy Default { get; } = new(256L * 1024 * 1024, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(10), 5, false);
}
