namespace DataFlow.Blazor.Services;

using DataFlow.Blazor.Api;

/// <summary>
/// Retrieves the list of all known flow runs from the server.
/// </summary>
public interface IFlowListSource
{
    Task<FlowSummaryDto[]> GetFlowsAsync(CancellationToken cancellationToken = default);
}
