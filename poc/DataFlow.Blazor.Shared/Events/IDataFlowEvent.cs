namespace DataFlow.Blazor.Events;

/// <summary>
/// Base interface for all DataFlow visualization events.
/// </summary>
public interface IDataFlowEvent
{
    DateTime Timestamp { get; }
}
