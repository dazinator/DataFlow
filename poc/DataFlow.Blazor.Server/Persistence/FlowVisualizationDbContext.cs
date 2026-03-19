namespace DataFlow.Blazor.Server.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Standalone EF Core DbContext for DataFlow visualization persistence.
///
/// Usage — register in your ASP.NET Core host:
/// <code>
/// builder.Services.AddDataFlowVisualizationServer(options =>
///     options.UseSqlite("Data Source=dataflow-viz.db"));
/// </code>
///
/// Or use your existing DbContext by calling AddEntityFrameworkStores&lt;TContext&gt;()
/// and inheriting from this context / adding the DbSets manually.
/// </summary>
public class FlowVisualizationDbContext : DbContext
{
    public FlowVisualizationDbContext(DbContextOptions<FlowVisualizationDbContext> options)
        : base(options) { }

    public DbSet<FlowEventRecord> FlowEventRecords => Set<FlowEventRecord>();
    public DbSet<FlowSnapshotRecord> FlowSnapshotRecords => Set<FlowSnapshotRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FlowEventRecord>(entity =>
        {
            entity.HasIndex(e => e.FlowRunId);
        });

        modelBuilder.Entity<FlowSnapshotRecord>(entity =>
        {
            entity.HasKey(e => e.FlowRunId);
        });
    }
}
