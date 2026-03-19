namespace DataFlow.Blazor.Server.Services;

using DataFlow.Blazor.Events;

/// <summary>
/// Determines when to materialize a FlowSnapshotRecord.
/// Snapshot on completion so future loads never re-fold the full log,
/// and periodically during long-running flows so the delta on reconnect stays small.
/// </summary>
public class SnapshotPolicy
{
    private readonly int _periodicInterval;

    /// <param name="periodicInterval">
    /// Materialize a snapshot every N events during a running flow (0 = disable periodic snapshots).
    /// </param>
    public SnapshotPolicy(int periodicInterval = 100)
    {
        _periodicInterval = periodicInterval;
    }

    public bool ShouldSnapshot(IDataFlowEvent evt, long currentSequence) =>
        evt is FlowCompletedEvent ||
        (_periodicInterval > 0 && currentSequence > 0 && currentSequence % _periodicInterval == 0);
}
