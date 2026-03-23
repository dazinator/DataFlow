namespace DataFlow.Blazor.Server.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// ModelBuilder extension for registering DataFlow visualization entities.
///
/// Call this from your own DbContext's OnModelCreating when using the
/// bring-your-own-context path:
/// <code>
/// protected override void OnModelCreating(ModelBuilder modelBuilder)
/// {
///     base.OnModelCreating(modelBuilder);
///     modelBuilder.AddDataFlowVisualizationEntities();
/// }
/// </code>
///
/// This is called automatically by <see cref="FlowVisualizationDbContext"/>
/// when using the dedicated-context path.
/// </summary>
public static class DataFlowModelBuilderExtensions
{
    public static ModelBuilder AddDataFlowVisualizationEntities(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FlowEventRecord>(entity =>
        {
            entity.HasIndex(e => e.FlowRunId);
            entity.HasIndex(e => e.CorrelationId);
        });

        modelBuilder.Entity<FlowSnapshotRecord>(entity =>
        {
            entity.HasKey(e => e.FlowRunId);
        });

        return modelBuilder;
    }
}
