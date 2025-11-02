namespace EpochAnchoringDemo.Models;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// DbContext for the demo data.
/// </summary>
public sealed class DemoDbContext : DbContext
{
    public DemoDbContext(DbContextOptions<DemoDbContext> options)
        : base(options)
    {
    }

    public DbSet<DataRecord> DataRecords => Set<DataRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Value).HasPrecision(18, 2);
            entity.HasIndex(e => e.Processed);
        });
    }
}
