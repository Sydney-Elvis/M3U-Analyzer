using System.Security.Cryptography;
using System.Text;

namespace M3UAnalyzer.Domain.Catalog;

/// <summary>Computes credential-free stream identifiers. The locator itself is never stored.</summary>
public static class StreamKeys
{
    /// <summary>
    /// Returns the first 32 lower-case hex characters of SHA-256(UTF-8("{contentType lower-case}|{locator}")).
    /// Throws <see cref="ArgumentNullException"/> for null and <see cref="ArgumentException"/> for empty/whitespace locators.
    /// </summary>
    public static string Compute(ContentType contentType, string locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        if (string.IsNullOrWhiteSpace(locator))
        {
            throw new ArgumentException("Locator must not be empty or whitespace.", nameof(locator));
        }

        var input = $"{contentType.ToString().ToLowerInvariant()}|{locator}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }
}