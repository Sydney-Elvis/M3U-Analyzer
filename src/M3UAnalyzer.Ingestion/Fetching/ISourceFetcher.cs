namespace M3UAnalyzer.Ingestion.Fetching;

public interface ISourceFetcher
{
    Task<FetchedContent> FetchUrlAsync(string url, FetchPolicy policy, CancellationToken cancellationToken);
}
