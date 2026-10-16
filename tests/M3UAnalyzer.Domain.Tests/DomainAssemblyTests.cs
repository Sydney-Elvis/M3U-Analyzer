using M3UAnalyzer.Domain;

namespace M3UAnalyzer.Domain.Tests;

[TestClass]
public sealed class DomainAssemblyTests
{
    [TestMethod]
    public void DomainAssembly_HasNoPackageOrProjectReferencesBeyondTheFramework()
    {
        var references = typeof(DomainAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(static name => name.Name ?? string.Empty)
            .Where(static name => !name.StartsWith("System", StringComparison.Ordinal)
                                  && name != "netstandard"
                                  && name != "mscorlib")
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), references);
    }
}
