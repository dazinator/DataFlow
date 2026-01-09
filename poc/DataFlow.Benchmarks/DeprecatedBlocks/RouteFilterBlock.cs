namespace DataFlow.POC.Benchmarks.DeprecatedBlocks;

using DataFlow.POC.Core;

/// <summary>
/// ⚠️ DEPRECATED - Benchmark-only. Use SelectiveRoutingEdgeStrategy for new code.
/// 
/// Route filter block that filters items by route key.
/// This block is retained solely for benchmark compatibility with the old routing pattern.
/// New code should use SelectiveRoutingEdgeStrategy instead.
/// </summary>
/// <typeparam name="T">Item type</typeparam>
/// <remarks>
/// This block was part of the pre-SelectiveRoutingEdgeStrategy architecture.
/// It is maintained only for benchmark compatibility.
/// The modern approach uses SelectiveRoutingEdgeStrategy which routes items directly
/// without needing separate filter blocks.
/// </remarks>
[Obsolete("RouteFilterBlock is deprecated. Use SelectiveRoutingEdgeStrategy for new code.")]
public sealed class RouteFilterBlock<T> : BlockBase<RoutedItem<T>, T>
{
    private readonly string _routeKey;

    /// <summary>
    /// Constructor for backward compatibility with benchmarks.
    /// </summary>
    /// <param name="name">Block name</param>
    /// <param name="routeKey">Route key to filter for</param>
    public RouteFilterBlock(string name, string routeKey)
        : base(new BlockContext(name))
    {
        _routeKey = routeKey ?? throw new ArgumentNullException(nameof(routeKey));
    }

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<RoutedItem<T>> input,
        IExecutionContext context)
    {
        await foreach (var routedItem in input.WithCancellation(context.CancellationToken))
        {
            if (routedItem.RouteKey == _routeKey)
            {
                yield return routedItem.Item;
            }
        }
    }
}
