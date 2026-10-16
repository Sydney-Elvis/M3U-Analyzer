using Microsoft.EntityFrameworkCore;

namespace M3UAnalyzer.Data.Tests.TestSupport;

public sealed class TestContextFactory(TempDatabase database) : IDbContextFactory<AnalyzerDbContext>
{
    public AnalyzerDbContext CreateDbContext() => database.CreateContext();
}

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
