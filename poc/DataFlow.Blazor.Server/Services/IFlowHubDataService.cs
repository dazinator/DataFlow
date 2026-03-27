namespace DataFlow.Blazor.Server.Services;

using DataFlow.Blazor.Api;

/// <summary>
/// Provides data access for <see cref="FlowEventsHub"/> gap-fill queries.
///
/// The default implementation, <see cref="EfCoreFlowHubDataService{TContext}"/>, resolves
/// the DbContext directly from DI and is registered automatically by
/// <see cref="DataFlowVisualizationServerExtensions.AddDataFlowVisualizationServer{TContext}"/>.
///
/// In multi-tenant or non-standard DI environments where the DbContext is not available
/// in the root service container, replace the default by registering your own implementation:
/// <code>
/// // Register BEFORE or AFTER AddDataFlowVisualizationServer — last registration wins.
/// builder.Services.AddScoped&lt;IFlowHubDataService, MyTenantAwareFlowHubDataService&gt;();
/// </code>
///
/// The implementation receives scoped lifetime (one instance per SignalR connection hub activation),
/// consistent with how ASP.NET Core activates hub instances.
/// </summary>
public interface IFlowHubDataService
{
    /// <summary>
    /// Returns all events for <paramref name="flowRunId"/> with an Id greater than
    /// <paramref name="fromId"/>, ordered ascending. Used to replay any events that
    /// arrived between the client's initial HTTP snapshot call and its SignalR subscription.
    /// </summary>
    Task<IReadOnlyList<FlowEventDto>> GetMissedEventsAsync(
        Guid flowRunId,
        long fromId,
        CancellationToken cancellationToken = default);
}
