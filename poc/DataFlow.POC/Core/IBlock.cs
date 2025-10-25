namespace DataFlow.POC.Core;

/// <summary>
/// Base interface for all blocks in the dataflow.
/// Blocks are pure units of computation that transform async streams.
/// </summary>
public interface IBlock
{
    /// <summary>
    /// The unique name of this block.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The input type of this block (object for source blocks with no input).
    /// </summary>
    Type InputType { get; }

    /// <summary>
    /// The output type of this block.
    /// </summary>
    Type OutputType { get; }

    /// <summary>
    /// Execute the block with given inputs and context.
    /// For source blocks, input will be an empty enumerable.
    /// For target blocks, this returns an empty enumerable.
    /// </summary>
    IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context);
}

/// <summary>
/// Typed block interface for compile-time type safety.
/// </summary>
public interface IBlock<TIn, TOut> : IBlock
{
    /// <summary>
    /// Execute the block with typed input and produce typed output.
    /// </summary>
    IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        IExecutionContext context);
}
