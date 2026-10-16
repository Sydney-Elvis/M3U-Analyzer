namespace M3UAnalyzer.Ingestion.Fetching;

/// <summary>Resolves a user-supplied file name inside the import directory.</summary>
public static class ImportFileResolver
{
    /// <summary>Returns the full path of <paramref name="fileName"/> inside <paramref name="importDirectory"/>.</summary>
    public static string Resolve(string importDirectory, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(importDirectory);

        if (string.IsNullOrWhiteSpace(fileName)
            || fileName != fileName.Trim()
            || fileName == "."
            || fileName == ".."
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || Path.IsPathRooted(fileName))
        {
            throw new FetchFailedException(FetchFailureKind.FileNotAllowed, "Only a plain file name inside the import directory is allowed.");
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(importDirectory));
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, fileName));

        if (!full.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new FetchFailedException(FetchFailureKind.FileNotAllowed, "Only a plain file name inside the import directory is allowed.");
        }

        if (!File.Exists(full))
        {
            throw new FetchFailedException(FetchFailureKind.FileNotFound, $"Import file '{fileName}' was not found.");
        }

        if (new FileInfo(full).LinkTarget is not null)
        {
            throw new FetchFailedException(FetchFailureKind.FileNotAllowed, "Symbolic links are not allowed in the import directory.");
        }

        return full;
    }
}
