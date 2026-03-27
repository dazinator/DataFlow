namespace DataFlow.Blazor.Server.Services;

using System.Security.Claims;
using DataFlow.Blazor.Api;

/// <summary>
/// Provides data access for <see cref="FlowEventsHub"/> gap-fill queries.
///
/// The default implementation, <see cref="EfCoreFlowHubDataService{TContext}"/>, resolves
/// the DbContext directly from DI and is registered automatically by
/// <see cref="DataFlowVisualizationServerExtensions.AddDataFlowVisualizationServer{TContext}"/>.
///
/// In apps that use per-tenant DI containers where the DbContext is not available
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
    /// <param name="flowRunId">The flow run to fetch missed events for.</param>
    /// <param name="fromId">Replay events with Id greater than this value.</param>
    /// <param name="user">
    /// The caller's <see cref="ClaimsPrincipal"/> from the SignalR hub connection
    /// (<c>Context.User</c>). Implementations can use this to resolve tenant identity
    /// from claims without requiring <c>IHttpContextAccessor</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<FlowEventDto>> GetMissedEventsAsync(
        Guid flowRunId,
        long fromId,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default);
}
