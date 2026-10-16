using M3UAnalyzer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace M3UAnalyzer.Data;

public sealed class AnalyzerDbContext(DbContextOptions<AnalyzerDbContext> options) : DbContext(options)
{
    public DbSet<SourceEntity> Sources => Set<SourceEntity>();
    public DbSet<SnapshotEntity> Snapshots => Set<SnapshotEntity>();
    public DbSet<SnapshotSectionEntity> SnapshotSections => Set<SnapshotSectionEntity>();
    public DbSet<ImportWarningEntity> ImportWarnings => Set<ImportWarningEntity>();
    public DbSet<CatalogGroupEntity> CatalogGroups => Set<CatalogGroupEntity>();
    public DbSet<ItemEntity> Items => Set<ItemEntity>();
    public DbSet<ItemGroupEntity> ItemGroups => Set<ItemGroupEntity>();
    public DbSet<EpisodeEntity> Episodes => Set<EpisodeEntity>();
    public DbSet<SeriesEpisodeLoadEntity> SeriesEpisodeLoads => Set<SeriesEpisodeLoadEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SourceEntity>(e =>
        {
            e.ToTable("Sources");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(36);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.DisplayLocation).HasMaxLength(500).IsRequired();
            e.Property(x => x.CurrentSnapshotId).HasMaxLength(36);
        });

        modelBuilder.Entity<SnapshotEntity>(e =>
        {
            e.ToTable("Snapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(36);
            e.Property(x => x.SourceId).HasMaxLength(36);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.CoreReference).HasMaxLength(64);
            e.Property(x => x.ErrorSummary).HasMaxLength(1000);
            e.HasIndex(x => new { x.SourceId, x.StartedUtc });
            e.HasOne<SourceEntity>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SnapshotSectionEntity>(e =>
        {
            e.ToTable("SnapshotSections");
            e.HasKey(x => new { x.SnapshotId, x.Section });
            e.Property(x => x.SnapshotId).HasMaxLength(36);
            e.Property(x => x.Section).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ErrorSummary).HasMaxLength(1000);
            e.HasOne<SnapshotEntity>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportWarningEntity>(e =>
        {
            e.ToTable("ImportWarnings");
            e.HasKey(x => x.Id);
            e.Property(x => x.SnapshotId).HasMaxLength(36);
            e.Property(x => x.Section).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.SnapshotId);
            e.HasOne<SnapshotEntity>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CatalogGroupEntity>(e =>
        {
            e.ToTable("CatalogGroups");
            e.HasKey(x => x.Id);
            e.Property(x => x.SnapshotId).HasMaxLength(36);
            e.Property(x => x.ContentType).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.RawName).IsRequired();
            e.HasIndex(x => new { x.SnapshotId, x.ContentType, x.RawName }).IsUnique();
            e.HasOne<SnapshotEntity>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemEntity>(e =>
        {
            e.ToTable("Items");
            e.HasKey(x => x.Id);
            e.Property(x => x.SnapshotId).HasMaxLength(36);
            e.Property(x => x.ContentType).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ContentTypeBasis).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.RawTitle).IsRequired();
            e.Property(x => x.DisplayTitle).IsRequired();
            e.Property(x => x.StreamKey).HasMaxLength(32);
            e.HasIndex(x => new { x.SnapshotId, x.ContentType });
            e.HasIndex(x => new { x.SnapshotId, x.TvgId });
            e.HasIndex(x => new { x.SnapshotId, x.ProviderItemId });
            e.HasIndex(x => new { x.SnapshotId, x.StreamKey });
            e.HasOne<SnapshotEntity>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemGroupEntity>(e =>
        {
            e.ToTable("ItemGroups");
            e.HasKey(x => new { x.ItemId, x.GroupId });
            e.HasIndex(x => x.GroupId);
            e.HasOne<ItemEntity>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<CatalogGroupEntity>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EpisodeEntity>(e =>
        {
            e.ToTable("Episodes");
            e.HasKey(x => x.Id);
            e.Property(x => x.SnapshotId).HasMaxLength(36);
            e.Property(x => x.ProviderEpisodeId).IsRequired();
            e.Property(x => x.Title).IsRequired();
            e.HasIndex(x => x.SeriesItemId);
            e.HasOne<SnapshotEntity>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ItemEntity>().WithMany().HasForeignKey(x => x.SeriesItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SeriesEpisodeLoadEntity>(e =>
        {
            e.ToTable("SeriesEpisodeLoads");
            e.HasKey(x => x.SeriesItemId);
            e.Property(x => x.SeriesItemId).ValueGeneratedNever();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ErrorSummary).HasMaxLength(1000);
            e.HasOne<ItemEntity>().WithOne().HasForeignKey<SeriesEpisodeLoadEntity>(x => x.SeriesItemId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
