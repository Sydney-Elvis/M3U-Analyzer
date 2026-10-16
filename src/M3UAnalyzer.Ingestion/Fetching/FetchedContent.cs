namespace M3UAnalyzer.Ingestion.Fetching;

/// <summary>A downloaded body stored in a temporary file. Dispose to delete the file.</summary>
public sealed class FetchedContent : IDisposable
{
    public FetchedContent(string tempFilePath, long bytes, string? contentType, bool wasGzipFile)
    {
        TempFilePath = tempFilePath;
        Bytes = bytes;
        ContentType = contentType;
        WasGzipFile = wasGzipFile;
    }

    public string TempFilePath { get; }

    public long Bytes { get; }

    public string? ContentType { get; }

    public bool WasGzipFile { get; }

    public Task<string> ReadAllTextAsync(CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(TempFilePath, cancellationToken);

    public void Dispose()
    {
        try
        {
            File.Delete(TempFilePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
