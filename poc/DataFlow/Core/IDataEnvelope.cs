namespace DataFlow.POC.Core;

/// <summary>
/// Base interface for all data envelopes in the dataflow.
/// Envelopes wrap data and control messages in a uniform type,
/// enabling in-band control signals to flow alongside data.
/// </summary>
public interface IDataEnvelope
{
}

/// <summary>
/// Envelope that wraps a data item of type T.
/// This is the standard envelope for user data flowing through the pipeline.
/// </summary>
/// <typeparam name="T">The type of the data item</typeparam>
public sealed record DataItem<T>(T Value) : IDataEnvelope;

/// <summary>
/// Control signal representing a checkpoint barrier.
/// Used for synchronizing DAG progress and enabling fault recovery.
/// All blocks must process and forward this barrier to ensure coordinated checkpointing.
/// </summary>
public sealed record CheckpointBarrier(Guid Id, DateTime CreatedAt) : IDataEnvelope;

/// <summary>
/// Control signal representing a heartbeat.
/// Used for progress tracking and liveness monitoring in long-running or real-time streams.
/// </summary>
public sealed record Heartbeat(DateTime Timestamp) : IDataEnvelope;

/// <summary>
/// Utility methods for working with envelopes.
/// </summary>
public static class EnvelopeExtensions
{
    /// <summary>
    /// Wraps a value in a DataItem envelope.
    /// </summary>
    public static IDataEnvelope ToEnvelope<T>(this T value)
        => new DataItem<T>(value);

    /// <summary>
    /// Determines if an envelope is a data item.
    /// </summary>
    public static bool IsDataItem(this IDataEnvelope envelope)
        => envelope.GetType().IsGenericType 
           && envelope.GetType().GetGenericTypeDefinition() == typeof(DataItem<>);

    /// <summary>
    /// Determines if an envelope is a control signal.
    /// </summary>
    public static bool IsControlSignal(this IDataEnvelope envelope)
        => !envelope.IsDataItem();

    /// <summary>
    /// Extracts the value from a DataItem envelope.
    /// Throws InvalidOperationException if the envelope is not a DataItem.
    /// </summary>
    public static T GetValue<T>(this IDataEnvelope envelope)
    {
        if (envelope is DataItem<T> dataItem)
        {
            return dataItem.Value;
        }
        throw new InvalidOperationException($"Envelope is not a DataItem<{typeof(T).Name}>");
    }

    /// <summary>
    /// Tries to extract the value from a DataItem envelope.
    /// Returns true if successful, false otherwise.
    /// </summary>
    public static bool TryGetValue<T>(this IDataEnvelope envelope, out T? value)
    {
        if (envelope is DataItem<T> dataItem)
        {
            value = dataItem.Value;
            return true;
        }
        value = default;
        return false;
    }
}
