namespace M3UAnalyzer.Domain.Security;

/// <summary>Thrown when a catalog item fails the persistence safety check. The message never contains a value.</summary>
public sealed class UnsafeCatalogDataException(int ordinal, string field)
    : Exception($"Catalog item {ordinal}: {field} failed the persistence safety check.")
{
    public int Ordinal { get; } = ordinal;
}
