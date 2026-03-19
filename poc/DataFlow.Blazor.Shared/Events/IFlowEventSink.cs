namespace DataFlow.Blazor.Events;

/// <summary>
/// Receives DataFlow events and persists them to an append-only event log.
/// Implementations are provided by the server integration package (Uniun.DataFlow.Blazor.Server).
/// Register via AddDataFlowVisualizationServer() in your ASP.NET Core host.
/// </summary>
public interface IFlowEventSink
{
    Task AppendAsync(Guid flowRunId, IDataFlowEvent evt, CancellationToken cancellationToken = default);
}
