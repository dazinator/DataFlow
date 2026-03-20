namespace DataFlow.Blazor.Server;

using DataFlow.Blazor.Events;
using DataFlow.Blazor.Server.Endpoints;
using DataFlow.Blazor.Server.Persistence;
using DataFlow.Blazor.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// ASP.NET Core integration for DataFlow Blazor visualization.
///
/// Minimal setup in Program.cs:
/// <code>
/// // 1. Register server services (choose your EF Core provider)
/// builder.Services.AddDataFlowVisualizationServer(options =>
///     options.UseSqlite("Data Source=dataflow-viz.db"));
///
/// // 2. Map endpoints and SignalR hub
/// app.MapDataFlowEndpoints();
/// app.MapHub&lt;FlowEventsHub&gt;("/hubs/flow-events");
///
/// // 3. (Optional) ensure the schema exists on startup
/// using (var scope = app.Services.CreateScope())
///     scope.ServiceProvider.GetRequiredService&lt;FlowVisualizationDbContext&gt;()
///         .Database.EnsureCreated();
/// </code>
///
/// The DataFlow engine will automatically emit events if IFlowEventSink is resolvable
/// from the IExecutionContext.ServiceProvider during graph execution.
/// </summary>
public static class DataFlowVisualizationServerExtensions
{
    /// <summary>
    /// Registers DataFlow visualization server services: EF Core DbContext,
    /// IFlowEventSink (EF Core implementation), SignalR, and snapshot policy.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureDb">Configure the EF Core provider (e.g. UseSqlite, UseSqlServer).</param>
    /// <param name="periodicSnapshotInterval">
    /// Materialize a snapshot every N events during long-running flows (default 100, 0 = disabled).
    /// </param>
    public static IServiceCollection AddDataFlowVisualizationServer(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDb,
        int periodicSnapshotInterval = 100)
    {
        services.AddDbContext<FlowVisualizationDbContext>(configureDb);
        services.AddSignalR();
        services.AddSingleton(new SnapshotPolicy(periodicSnapshotInterval));
        services.AddScoped<IFlowEventSink, EfCoreFlowEventSink>();
        return services;
    }

    /// <summary>
    /// Maps the DataFlow visualization catch-up HTTP endpoint:
    ///   GET /flows/{flowRunId}/state
    /// </summary>
    public static IEndpointRouteBuilder MapDataFlowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapFlowStateEndpoints();
        app.MapFlowListEndpoints();
        return app;
    }
}
