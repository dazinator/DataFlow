namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;

/// <summary>
/// Broadcast block that duplicates items to multiple downstream targets.
/// In the new design, broadcasting is handled by the edge layer - a single
/// block can have multiple outgoing edges that each receive a copy of the items.
/// This is a pass-through block that demonstrates the concept.
/// </summary>
public class BroadcastBlock<T> : BlockBase<T, T>
{
    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public BroadcastBlock(IBlockContext context)
        : base(context)
    {
    }

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        // Simple pass-through - the graph's edge routing handles actual broadcasting
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return item;
        }
    }
}
