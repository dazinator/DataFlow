namespace DataFlow.POC.Core;

/// <summary>
/// Defines an actor that processes a stream of items within a scoped execution context.
/// Actors can request rotation to refresh their DI scope when needed (e.g., for memory management).
/// </summary>
/// <typeparam name="TIn">Input item type</typeparam>
/// <typeparam name="TOut">Output item type</typeparam>
public interface IStreamActor<TIn, TOut>
{
    /// <summary>
    /// Process the input stream and produce output items.
    /// The actor can request rotation via context.RequestRotation() when appropriate.
    /// </summary>
    /// <param name="input">Input stream to process</param>
    /// <param name="context">Execution context with rotation capability</param>
    /// <returns>Stream of processed items</returns>
    IAsyncEnumerable<TOut> RunAsync(
        IAsyncEnumerable<TIn> input,
        IActorExecutionContext context);
}
