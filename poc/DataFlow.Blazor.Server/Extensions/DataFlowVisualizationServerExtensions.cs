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
/// --- Option A: dedicated DbContext (simplest, no existing EF setup required) ---
/// <code>
/// // 1. Register SignalR, then DataFlow services — choose your EF Core provider
/// builder.Services.AddSignalR(); // or AddAzureSignalR()
/// builder.Services.AddDataFlowVisualizationServer(options =>
///     options.UseSqlite("Data Source=dataflow-viz.db"));
///
/// // 2. Map endpoints and SignalR hub
/// app.MapDataFlowEndpoints();
///
/// // 3. (Optional) ensure the schema exists on startup
/// using (var scope = app.Services.CreateScope())
///     scope.ServiceProvider.GetRequiredService&lt;FlowVisualizationDbContext&gt;()
///         .Database.EnsureCreated();
/// </code>
///
/// --- Option B: bring your own DbContext ---
/// <code>
/// // 1a. Register your DbContext as normal
/// builder.Services.AddDbContext&lt;MyAppDbContext&gt;(options =>
///     options.UseSqlServer(connectionString));
///
/// // 1b. Call AddDataFlowVisualizationEntities() in your DbContext's OnModelCreating:
/// //     modelBuilder.AddDataFlowVisualizationEntities();
///
/// // 2. Register SignalR, then DataFlow services pointing at your context
/// builder.Services.AddSignalR(); // or AddAzureSignalR()
/// builder.Services.AddDataFlowVisualizationServer&lt;MyAppDbContext&gt;();
///
/// // 3. Map endpoints and SignalR hub
/// app.MapDataFlowEndpoints&lt;MyAppDbContext&gt;();
/// </code>
///
/// The DataFlow engine emits events automatically if IFlowEventSink is resolvable
/// from the IExecutionContext.ServiceProvider during graph execution.
/// </summary>
public static class DataFlowVisualizationServerExtensions
{
    /// <summary>
    /// Registers DataFlow visualization services using a dedicated
    /// <see cref="FlowVisualizationDbContext"/>. Use this when you don't have an
    /// existing DbContext to merge into.
    /// </summary>
    /// <remarks>
    /// SignalR must be registered separately by the application before calling this method.
    /// See <see cref="AddDataFlowVisualizationServer{TContext}(IServiceCollection,int)"/> for details.
    /// </remarks>
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
        return services.AddDataFlowVisualizationServer<FlowVisualizationDbContext>(periodicSnapshotInterval);
    }

    /// <summary>
    /// Registers DataFlow visualization services using an existing <typeparamref name="TContext"/>.
    /// The context must have <see cref="DataFlowModelBuilderExtensions.AddDataFlowVisualizationEntities"/>
    /// called in its OnModelCreating, and must already be registered in the service collection.
    /// </summary>
    /// <remarks>
    /// SignalR must be registered separately by the application before calling this method
    /// (e.g. <c>services.AddSignalR()</c> or <c>services.AddAzureSignalR()</c>). This keeps
    /// SignalR configuration — provider, options, Azure connection strings — under the
    /// application's control and avoids shadowing a root-level Azure SignalR singleton with
    /// an in-process instance in per-tenant child containers.
    /// </remarks>
    /// <typeparam name="TContext">Your application's DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="periodicSnapshotInterval">
    /// Materialize a snapshot every N events during long-running flows (default 100, 0 = disabled).
    /// </param>
    public static IServiceCollection AddDataFlowVisualizationServer<TContext>(
        this IServiceCollection services,
        int periodicSnapshotInterval = 100)
        where TContext : DbContext
    {
        services.AddSingleton(new SnapshotPolicy(periodicSnapshotInterval));
        services.AddScoped<IFlowEventSink, EfCoreFlowEventSink<TContext>>();
        // Register default hub data service. Replace with a custom IFlowHubDataService
        // implementation if TContext is not resolvable from the root container
        // (e.g. apps that use per-tenant DI containers).
        services.AddScoped<IFlowHubDataService, EfCoreFlowHubDataService<TContext>>();
        return services;
    }

    /// <summary>
    /// Maps the DataFlow HTTP endpoints only (no SignalR hub).
    /// Use this when you want to register the hub yourself — for example to apply
    /// auth policies, Azure SignalR options, or a custom path:
    /// <code>
    /// app.MapDataFlowHttpEndpoints();
    /// app.MapHub&lt;FlowEventsHub&gt;("/my/hub/path");
    /// </code>
    /// The client must be configured with the matching hub path:
    /// <code>
    /// builder.Services.AddDataFlowVisualizationClient(
    ///     baseUrl: builder.HostEnvironment.BaseAddress,
    ///     hubPath: "/my/hub/path");
    /// </code>
    /// </summary>
    public static IEndpointRouteBuilder MapDataFlowHttpEndpoints(
        this IEndpointRouteBuilder app)
        => app.MapDataFlowHttpEndpoints<FlowVisualizationDbContext>();

    /// <summary>
    /// Maps the DataFlow HTTP endpoints only (no SignalR hub) using <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TContext">Your application's DbContext type.</typeparam>
    public static IEndpointRouteBuilder MapDataFlowHttpEndpoints<TContext>(
        this IEndpointRouteBuilder app)
        where TContext : DbContext
    {
        app.MapFlowStateEndpoints<TContext>();
        app.MapFlowListEndpoints<TContext>();
        return app;
    }

    /// <summary>
    /// Maps DataFlow HTTP endpoints and the SignalR hub using the dedicated
    /// <see cref="FlowVisualizationDbContext"/>. Call after <see cref="AddDataFlowVisualizationServer(IServiceCollection, Action{DbContextOptionsBuilder}, int)"/>.
    /// If you need to control the hub registration (custom path, auth, Azure SignalR options)
    /// use <see cref="MapDataFlowHttpEndpoints"/> and call <c>MapHub</c> yourself.
    /// </summary>
    /// <param name="hubPath">SignalR hub path (default: /hubs/flow-events). Must match the client registration.</param>
    public static IEndpointRouteBuilder MapDataFlowEndpoints(
        this IEndpointRouteBuilder app,
        string hubPath = "/hubs/flow-events")
    {
        app.MapDataFlowHttpEndpoints();
        app.MapHub<FlowEventsHub>(hubPath);
        return app;
    }

    /// <summary>
    /// Maps DataFlow HTTP endpoints and the SignalR hub using <typeparamref name="TContext"/>.
    /// Call after <see cref="AddDataFlowVisualizationServer{TContext}(IServiceCollection, int)"/>.
    /// If you need to control the hub registration (custom path, auth, Azure SignalR options)
    /// use <see cref="MapDataFlowHttpEndpoints{TContext}"/> and call <c>MapHub</c> yourself.
    /// </summary>
    /// <typeparam name="TContext">Your application's DbContext type.</typeparam>
    /// <param name="hubPath">SignalR hub path (default: /hubs/flow-events). Must match the client registration.</param>
    public static IEndpointRouteBuilder MapDataFlowEndpoints<TContext>(
        this IEndpointRouteBuilder app,
        string hubPath = "/hubs/flow-events")
        where TContext : DbContext
    {
        app.MapDataFlowHttpEndpoints<TContext>();
        app.MapHub<FlowEventsHub>(hubPath);
        return app;
    }
}
