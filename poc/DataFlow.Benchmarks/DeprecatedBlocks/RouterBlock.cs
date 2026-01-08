namespace DataFlow.POC.Benchmarks.DeprecatedBlocks;

using DataFlow.POC.Core;

/// <summary>
/// ⚠️ DEPRECATED - Benchmark-only. Use SelectiveRoutingEdgeStrategy for new code.
/// 
/// Router block that wraps items with route keys for routing.
/// This block is retained solely for benchmark compatibility with the old routing pattern.
/// New code should use SelectiveRoutingEdgeStrategy instead.
/// </summary>
/// <typeparam name="T">Item type</typeparam>
/// <remarks>
/// This block was part of the pre-SelectiveRoutingEdgeStrategy architecture.
/// It is maintained only for benchmark compatibility.
/// The modern approach uses SelectiveRoutingEdgeStrategy which routes items directly
/// without wrapping them in RoutedItem records.
/// </remarks>
[Obsolete("RouterBlock is deprecated. Use SelectiveRoutingEdgeStrategy for new code.")]
public sealed class RouterBlock<T> : BlockBase<T, RoutedItem<T>>
{
    private readonly Func<T, string> _routeSelector;

    /// <summary>
    /// Constructor for backward compatibility with benchmarks.
    /// </summary>
    /// <param name="name">Block name</param>
    /// <param name="routeSelector">Function to extract route key from item</param>
    public RouterBlock(string name, Func<T, string> routeSelector)
        : base(new BlockContext(name))
    {
        _routeSelector = routeSelector ?? throw new ArgumentNullException(nameof(routeSelector));
    }

    public override async IAsyncEnumerable<RoutedItem<T>> ExecuteAsync(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            var routeKey = _routeSelector(item);
            yield return new RoutedItem<T>(routeKey, item);
        }
    }
}

/// <summary>
/// Wrapper record for routed items (used by deprecated RouterBlock).
/// </summary>
/// <typeparam name="T">Item type</typeparam>
public record RoutedItem<T>(string RouteKey, T Item);
