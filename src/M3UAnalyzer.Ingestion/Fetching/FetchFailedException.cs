namespace M3UAnalyzer.Ingestion.Fetching;

/// <summary>Why a fetch failed. Persisted as text; never rename members.</summary>
public enum FetchFailureKind { InvalidUrl, BlockedAddress, HttpStatus, Timeout, TooLarge, TooManyRedirects, Network, FileNotAllowed, FileNotFound }

/// <summary>A fetch failure whose <see cref="Exception.Message"/> is already redacted and safe to log or show.</summary>
public sealed class FetchFailedException(FetchFailureKind kind, string redactedMessage, Exception? innerException = null)
    : Exception(redactedMessage, innerException)
{
    public FetchFailureKind Kind { get; } = kind;
}
