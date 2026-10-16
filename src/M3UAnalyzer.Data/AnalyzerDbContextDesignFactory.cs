using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace M3UAnalyzer.Data;

/// <summary>Used only by <c>dotnet ef</c> to create migrations. Never used at runtime.</summary>
public sealed class AnalyzerDbContextDesignFactory : IDesignTimeDbContextFactory<AnalyzerDbContext>
{
    public AnalyzerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AnalyzerDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
