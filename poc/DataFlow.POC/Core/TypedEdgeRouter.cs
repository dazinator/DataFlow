namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Channels;

/// <summary>
/// Factory for creating typed edge routers that eliminate boxing overhead.
/// Uses compiled generic delegates to route items through typed channels without boxing value types.
/// </summary>
public static class TypedEdgeRouterFactory
{
    // Cache for compiled router delegates: Type -> Func that creates a router for that type
    private static readonly ConcurrentDictionary<Type, Func<Edge, Dictionary<IBlock, object>, ITypedEdgeRouter>> _routerFactoryCache = new();

    /// <summary>
    /// Creates a typed edge router for the specified data type.
    /// Uses reflection with caching to create a strongly-typed router that avoids boxing.
    /// </summary>
    /// <param name="dataType">The type of data flowing through the edge</param>
    /// <param name="edge">The edge to route</param>
    /// <param name="typedWriters">Dictionary of typed channel writers (as objects)</param>
    /// <returns>A typed edge router</returns>
    public static ITypedEdgeRouter CreateTypedRouter(Type dataType, Edge edge, Dictionary<IBlock, object> typedWriters)
    {
        var factory = _routerFactoryCache.GetOrAdd(dataType, type =>
        {
            // We need to create an instance of TypedEdgeRouter<T> where T is determined at runtime.
            // Since we can't use 'new TypedEdgeRouter<T>(...)' directly (T is not known at compile time),
            // we use Expression Trees to build a compiled delegate that creates the router.
            //
            // This is equivalent to generating the following at runtime:
            // (Edge edge, Dictionary writers) => new TypedEdgeRouter<T>(edge, writers)
            //
            // The compiled delegate is cached, so this overhead only occurs once per type.
            
            var routerType = typeof(TypedEdgeRouter<>).MakeGenericType(type);
            var constructor = routerType.GetConstructor(new[] { typeof(Edge), typeof(Dictionary<IBlock, object>) })
                ?? throw new InvalidOperationException($"Could not find constructor for {routerType.Name}");

            // Define parameters for the lambda: (edge, writers) => ...
            var edgeParam = Expression.Parameter(typeof(Edge), "edge");
            var writersParam = Expression.Parameter(typeof(Dictionary<IBlock, object>), "writers");
            
            // Build the constructor call: new TypedEdgeRouter<T>(edge, writers)
            var newExpr = Expression.New(constructor, edgeParam, writersParam);
            
            // Compile to a delegate
            var lambda = Expression.Lambda<Func<Edge, Dictionary<IBlock, object>, ITypedEdgeRouter>>(
                newExpr, edgeParam, writersParam);
            return lambda.Compile();
        });

        return factory(edge, typedWriters);
    }
}

/// <summary>
/// Non-generic interface for typed edge routers.
/// Allows polymorphic handling of routers for different types.
/// </summary>
public interface ITypedEdgeRouter
{
    /// <summary>
    /// Routes a single item through the edge strategy.
    /// </summary>
    Task RouteItemAsync(object item, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets the item type this router handles.
    /// </summary>
    Type ItemType { get; }
    
    /// <summary>
    /// Gets the target blocks for this edge.
    /// </summary>
    IReadOnlyList<IBlock> TargetBlocks { get; }
    
    /// <summary>
    /// Gets the edge strategy used by this router.
    /// </summary>
    EdgeStrategy Strategy { get; }
}

/// <summary>
/// Generic typed edge router that eliminates boxing by working with typed channels.
/// Routes items of type T through Channel&lt;T&gt; without converting to object.
/// Delegates actual routing logic to the EdgeStrategy.
/// </summary>
/// <typeparam name="T">The type of items flowing through the edge</typeparam>
public sealed class TypedEdgeRouter<T> : ITypedEdgeRouter
{
    private readonly Edge _edge;
    private readonly Dictionary<IBlock, ChannelWriter<T>> _typedWriters;

    /// <summary>
    /// Creates a typed edge router with strongly-typed channel writers.
    /// Extracts ChannelWriter&lt;T&gt; from object dictionaries to enable boxing-free writes.
    /// </summary>
    public TypedEdgeRouter(Edge edge, Dictionary<IBlock, object> writers)
    {
        _edge = edge ?? throw new ArgumentNullException(nameof(edge));
        
        // Extract typed writers from object dictionary
        _typedWriters = new Dictionary<IBlock, ChannelWriter<T>>();
        foreach (var (block, writerObj) in writers)
        {
            if (writerObj is not ChannelWriter<T> typedWriter)
            {
                throw new InvalidOperationException(
                    $"Expected ChannelWriter<{typeof(T).Name}> but got {writerObj.GetType().Name}");
            }
            _typedWriters[block] = typedWriter;
        }
    }
    
    /// <summary>
    /// Gets the item type this router handles.
    /// </summary>
    public Type ItemType => typeof(T);
    
    /// <summary>
    /// Gets the target blocks for this edge.
    /// </summary>
    public IReadOnlyList<IBlock> TargetBlocks => _edge.TargetBlocks;
    
    /// <summary>
    /// Gets the edge strategy used by this router.
    /// </summary>
    public EdgeStrategy Strategy => _edge.Strategy;

    /// <summary>
    /// Routes a single item using strongly-typed channels.
    /// The item is passed as object (boxing for value types), then cast to T (unboxing).
    /// After that, no boxing occurs during channel writes.
    /// For zero-boxing routing, use the internal RouteTypedItemAsync method.
    /// </summary>
    public async Task RouteItemAsync(object item, CancellationToken cancellationToken)
    {
        // Cast to T (unboxing happens here once, not per channel write)
        var typedItem = (T)item;
        
        // Delegate to strategy which handles the routing logic (including any cloning/transformation)
        await _edge.Strategy.RouteTypedItemAsync(typedItem, _typedWriters, cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Routes a typed item directly without boxing. Internal use only by typed execution.
    /// </summary>
    internal async Task RouteTypedItemAsync(T item, CancellationToken cancellationToken)
    {
        // Delegate to strategy - NO BOXING
        await _edge.Strategy.RouteTypedItemAsync(item, _typedWriters, cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Routes a typed item directly to a specific target block, bypassing the strategy.
    /// Used internally for epoch stream unwrap/wrap container routing.
    /// Note: Only called once per epoch stream container, not per data item,
    /// so the dictionary lookup overhead is minimal.
    /// </summary>
    internal async Task RouteToSpecificTargetAsync(T item, IBlock targetBlock, CancellationToken cancellationToken)
    {
        if (_typedWriters.TryGetValue(targetBlock, out var writer))
        {
            await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException($"Target block {targetBlock.Name} is not a valid target for this router");
        }
    }
}
