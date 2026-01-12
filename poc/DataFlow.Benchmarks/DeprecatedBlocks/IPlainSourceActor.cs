namespace DataFlow.POC.Core;

/// <summary>
/// ⚠️ DEPRECATED - Benchmark-only interface.
/// 
/// Interface for a plain source actor that produces continuous data streams
/// without epoch knowledge. This interface is retained solely for backward
/// compatibility with deprecated benchmark code.
/// 
/// New code should use ISourceActor&lt;T&gt; instead which produces epoch streams.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
[Obsolete("IPlainSourceActor is deprecated. Use ISourceActor<T> for new code.")]
public interface IPlainSourceActor<T>
{
    /// <summary>
    /// Produces a continuous stream of data items without epoch boundaries.
    /// </summary>
    /// <param name="context">Execution context with cancellation support</param>
    /// <returns>A continuous stream of items</returns>
    IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context);
}

/// <summary>
/// ⚠️ DEPRECATED - Benchmark-only base class.
/// 
/// Base class for plain source actors that don't know about epochs.
/// This class is retained solely for backward compatibility with deprecated benchmark code.
/// </summary>
[Obsolete("PlainSourceActorBase is deprecated. Use ISourceActor<T> for new code.")]
public abstract class PlainSourceActorBase<T> : IPlainSourceActor<T>
{
    /// <summary>
    /// Produces a continuous stream of items. Subclasses override this to implement their data production logic.
    /// </summary>
    public abstract IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context);
}
