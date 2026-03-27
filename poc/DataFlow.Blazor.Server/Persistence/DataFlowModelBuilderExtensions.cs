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
///
/// <para>
/// <b>Multi-tenant deployments:</b> The DataFlow entities do not include a
/// <c>TenantId</c> CLR property so that applications can configure tenant
/// isolation using a shadow property of whatever type their data model requires
/// (e.g. <c>int</c>, <c>Guid</c>, <c>string</c>).
/// After calling <see cref="AddDataFlowVisualizationEntities"/>, configure the
/// shadow property and global query filter for each entity in your own
/// <c>OnModelCreating</c>. For example, using an <c>int</c> shadow property:
/// <code>
/// modelBuilder.AddDataFlowVisualizationEntities();
/// modelBuilder.Entity&lt;FlowEventRecord&gt;()
///     .Property&lt;int&gt;("TenantId");
/// modelBuilder.Entity&lt;FlowEventRecord&gt;()
///     .HasQueryFilter(e => EF.Property&lt;int&gt;(e, "TenantId") == tenantId);
/// modelBuilder.Entity&lt;FlowSnapshotRecord&gt;()
///     .Property&lt;int&gt;("TenantId");
/// modelBuilder.Entity&lt;FlowSnapshotRecord&gt;()
///     .HasQueryFilter(e => EF.Property&lt;int&gt;(e, "TenantId") == tenantId);
/// </code>
/// </para>
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
            // Supports the flow-list query: ORDER BY AsOfEventId DESC (most-recent first)
            entity.HasIndex(e => e.AsOfEventId).IsDescending();
        });

        return modelBuilder;
    }
}
