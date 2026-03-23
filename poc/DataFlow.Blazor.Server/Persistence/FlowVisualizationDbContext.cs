namespace DataFlow.Blazor.Server.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Standalone EF Core DbContext for DataFlow visualization persistence.
/// Use this when you don't have an existing DbContext to merge into.
///
/// Register in Program.cs:
/// <code>
/// builder.Services.AddDataFlowVisualizationServer(options =>
///     options.UseSqlite("Data Source=dataflow-viz.db"));
/// </code>
///
/// To use your own existing DbContext instead, see
/// <see cref="DataFlowModelBuilderExtensions.AddDataFlowVisualizationEntities"/>.
/// </summary>
public class FlowVisualizationDbContext : DbContext
{
    public FlowVisualizationDbContext(DbContextOptions<FlowVisualizationDbContext> options)
        : base(options) { }

    public DbSet<FlowEventRecord> FlowEventRecords => Set<FlowEventRecord>();
    public DbSet<FlowSnapshotRecord> FlowSnapshotRecords => Set<FlowSnapshotRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.AddDataFlowVisualizationEntities();
}
