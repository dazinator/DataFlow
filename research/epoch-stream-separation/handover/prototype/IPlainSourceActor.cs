namespace DataFlow.POC.Core;

/// <summary>
/// PROTOTYPE: Interface for a plain source actor that produces continuous data streams
/// without epoch knowledge. This is part of the "decoupled epoch" research.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
public interface IPlainSourceActor<T>
{
    /// <summary>
    /// Produces a continuous stream of data items without epoch boundaries.
    /// Epoch segmentation, if needed, is applied externally via EpochSegmenterBlock.
    /// </summary>
    /// <param name="context">Execution context with cancellation support</param>
    /// <returns>A continuous stream of items</returns>
    IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context);
}

/// <summary>
/// PROTOTYPE: Base class for plain source actors that don't know about epochs.
/// </summary>
public abstract class PlainSourceActorBase<T> : IPlainSourceActor<T>
{
    /// <summary>
    /// Produces a continuous stream of items. Subclasses override this to implement their data production logic.
    /// </summary>
    public abstract IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context);
}
