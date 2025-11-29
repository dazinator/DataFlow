namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Channels;

/// <summary>
/// Centralizes reflection operations used throughout the DataFlow execution pipeline.
/// Provides type-safe wrappers around reflection calls with clear error handling.
/// All reflection operations are documented with equivalent typed code examples.
/// </summary>
public static class ReflectionHelper
{
    /// <summary>
    /// Cache for epoch stream type detection to avoid repeated reflection.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, bool> _isEpochStreamTypeCache = new();
    
    /// <summary>
    /// Cache for compiled epoch stream routing delegates to avoid repeated reflection.
    /// Key is the item type (TItem), value is the compiled delegate.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<object, List<ITypedEdgeRouter>, CancellationToken, Task>> 
        _epochRoutingCache = new();
    
    /// <summary>
    /// Cache for compiled container routing delegates to avoid repeated reflection.
    /// Key is the item type (TItem), value is the compiled delegate that can call RouteToSpecificTargetAsync.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<ITypedEdgeRouter, object, IBlock, CancellationToken, Task>> 
        _containerRoutingCache = new();
    
    /// <summary>
    /// Determines if a type is IEpochStream&lt;T&gt; for some T.
    /// Uses caching to minimize reflection overhead.
    /// </summary>
    private static bool IsEpochStreamType(Type type)
    {
        return _isEpochStreamTypeCache.GetOrAdd(type, t =>
            t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEpochStream<>));
    }
    
    /// <summary>
    /// Extracts the item type from IEpochStream&lt;T&gt;.
    /// </summary>
    private static Type GetEpochStreamItemType(Type epochStreamType)
    {
        if (!IsEpochStreamType(epochStreamType))
        {
            throw new ArgumentException($"Type {epochStreamType} is not IEpochStream<T>", nameof(epochStreamType));
        }
        return epochStreamType.GetGenericArguments()[0];
    }
    
    /// <summary>
    /// Creates a compiled epoch stream routing delegate for a given edge data type.
    /// This should be called once at graph build time if the edge routes epoch streams.
    /// Returns null if the type is not an epoch stream type.
    /// </summary>
    public static Func<object, List<ITypedEdgeRouter>, CancellationToken, Task>? CreateEpochStreamRoutingDelegate(Type edgeDataType)
    {
        if (!IsEpochStreamType(edgeDataType))
        {
            return null;
        }
        
        var itemType = GetEpochStreamItemType(edgeDataType);
        
        // Build the delegate using reflection (this happens once at graph build time)
        var method = typeof(ReflectionHelper).GetMethod(
            nameof(EnumerateAndRouteEpochStreamAsync),
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
        {
            throw new InvalidOperationException($"Could not find method {nameof(EnumerateAndRouteEpochStreamAsync)}");
        }
        
        var genericMethod = method.MakeGenericMethod(itemType);
        
        // Create a compiled delegate that wraps the method invocation
        return (stream, rtrs, ct) => (Task)genericMethod.Invoke(null, new object[] { stream, rtrs, ct })!;
    }
    
    /// <summary>
    /// Creates a compiled container routing delegate for epoch stream containers.
    /// This eliminates dynamic casts and type checking during epoch stream routing.
    /// Returns null if the type is not an epoch stream type.
    /// Uses caching to avoid repeated compilation for the same item type.
    /// 
    /// The delegate signature is: (router, container, targetBlock, cancellationToken) => Task
    /// This allows calling TypedEdgeRouter&lt;IEpochStream&lt;TItem&gt;&gt;.RouteToSpecificTargetAsync without dynamic casts.
    /// </summary>
    public static Func<ITypedEdgeRouter, object, IBlock, CancellationToken, Task>? CreateContainerRoutingDelegate(Type edgeDataType)
    {
        if (!IsEpochStreamType(edgeDataType))
        {
            return null;
        }
        
        var itemType = GetEpochStreamItemType(edgeDataType);
        
        // Use cached delegate if available
        return _containerRoutingCache.GetOrAdd(itemType, type =>
        {
            // We need to create a delegate that can call:
            // TypedEdgeRouter<IEpochStream<TItem>>.RouteToSpecificTargetAsync(container, targetBlock, cancellationToken)
            
            // Build the typed method call using expression trees
            var routerParam = Expression.Parameter(typeof(ITypedEdgeRouter), "router");
            var containerParam = Expression.Parameter(typeof(object), "container");
            var targetBlockParam = Expression.Parameter(typeof(IBlock), "targetBlock");
            var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
            
            // Create the specific router type: TypedEdgeRouter<IEpochStream<TItem>>
            var epochStreamType = typeof(IEpochStream<>).MakeGenericType(type);
            var routerType = typeof(TypedEdgeRouter<>).MakeGenericType(epochStreamType);
            
            // Cast router to TypedEdgeRouter<IEpochStream<TItem>>
            var typedRouterExpr = Expression.Convert(routerParam, routerType);
            
            // Cast container to IEpochStream<TItem>
            var typedContainerExpr = Expression.Convert(containerParam, epochStreamType);
            
            // Get the RouteToSpecificTargetAsync method
            var methodInfo = routerType.GetMethod(
                "RouteToSpecificTargetAsync",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { epochStreamType, typeof(IBlock), typeof(CancellationToken) },
                null);
            
            if (methodInfo == null)
            {
                throw new InvalidOperationException($"Could not find RouteToSpecificTargetAsync method on {routerType.Name}");
            }
            
            // Build the method call: ((TypedEdgeRouter<IEpochStream<TItem>>)router).RouteToSpecificTargetAsync((IEpochStream<TItem>)container, targetBlock, cancellationToken)
            var callExpr = Expression.Call(
                typedRouterExpr,
                methodInfo,
                typedContainerExpr,
                targetBlockParam,
                cancellationTokenParam);
            
            // Compile to a delegate
            var lambda = Expression.Lambda<Func<ITypedEdgeRouter, object, IBlock, CancellationToken, Task>>(
                callExpr,
                routerParam,
                containerParam,
                targetBlockParam,
                cancellationTokenParam);
            
            return lambda.Compile();
        });
    }
    
    /// <summary>
    /// Creates an empty typed stream for source blocks with no input.
    /// Equivalent to: return AsyncEnumerable.Empty&lt;T&gt;();
    /// </summary>
    /// <param name="itemType">The item type for the stream</param>
    /// <returns>An empty IAsyncEnumerable&lt;T&gt; as object</returns>
    public static object CreateEmptyTypedStream(Type itemType)
    {
        // Equivalent to calling: CreateEmptyTypedStreamGeneric<T>()
        var method = typeof(ReflectionHelper).GetMethod(
            nameof(CreateEmptyTypedStreamGeneric),
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
        {
            throw new InvalidOperationException($"Could not find method {nameof(CreateEmptyTypedStreamGeneric)}");
        }
        
        var genericMethod = method.MakeGenericMethod(itemType);
        return genericMethod.Invoke(null, null)
            ?? throw new InvalidOperationException("CreateEmptyTypedStream returned null");
    }
    
    private static async IAsyncEnumerable<T> CreateEmptyTypedStreamGeneric<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
    
    /// <summary>
    /// Gets a typed stream from a channel reader by calling ReadAllAsync.
    /// Equivalent to: return ((ChannelReader&lt;T&gt;)readerObj).ReadAllAsync(cancellationToken);
    /// </summary>
    /// <param name="readerObj">The typed ChannelReader&lt;T&gt; as object</param>
    /// <param name="itemType">The item type T</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>IAsyncEnumerable&lt;T&gt; as object</returns>
    public static object GetTypedStreamFromChannelReader(
        object readerObj,
        Type itemType,
        CancellationToken cancellationToken)
    {
        // Equivalent to: return ((ChannelReader<T>)readerObj).ReadAllAsync(cancellationToken);
        var readerType = typeof(ChannelReader<>).MakeGenericType(itemType);
        var readAllAsyncMethod = readerType.GetMethod("ReadAllAsync");
        
        if (readAllAsyncMethod == null)
        {
            throw new InvalidOperationException($"Could not find ReadAllAsync method on {readerType.Name}");
        }
        
        var typedEnumerable = readAllAsyncMethod.Invoke(readerObj, new object[] { cancellationToken });
        return typedEnumerable
            ?? throw new InvalidOperationException("ReadAllAsync returned null");
    }
    
    /// <summary>
    /// Merges multiple typed streams into one.
    /// Equivalent to: return MergeAsyncEnumerables(typedStreams.Cast&lt;IAsyncEnumerable&lt;T&gt;&gt;().ToList());
    /// </summary>
    /// <param name="typedStreams">List of typed streams as objects</param>
    /// <param name="itemType">The item type T</param>
    /// <returns>Merged IAsyncEnumerable&lt;T&gt; as object</returns>
    public static object MergeTypedStreams(List<object> typedStreams, Type itemType)
    {
        // Equivalent to: return MergeTypedStreamsGeneric<T>(typedStreams);
        var method = typeof(ReflectionHelper).GetMethod(
            nameof(MergeTypedStreamsGeneric),
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
        {
            throw new InvalidOperationException($"Could not find method {nameof(MergeTypedStreamsGeneric)}");
        }
        
        var genericMethod = method.MakeGenericMethod(itemType);
        return genericMethod.Invoke(null, new object[] { typedStreams })
            ?? throw new InvalidOperationException("MergeTypedStreams returned null");
    }
    
    private static IAsyncEnumerable<T> MergeTypedStreamsGeneric<T>(List<object> typedStreams)
    {
        var typed = typedStreams.Cast<IAsyncEnumerable<T>>().ToList();
        return MergeAsyncEnumerables(typed);
    }
    
    /// <summary>
    /// Merges multiple async enumerables into one using concurrent reading.
    /// Uses a channel-based approach to read from all sources concurrently,
    /// enabling proper concurrency scaling when multiple blocks feed into one downstream block.
    /// </summary>
    private static async IAsyncEnumerable<T> MergeAsyncEnumerables<T>(List<IAsyncEnumerable<T>> sources)
    {
        if (sources.Count == 0)
        {
            yield break;
        }
        
        if (sources.Count == 1)
        {
            // Optimization: no merge needed for single source
            await foreach (var item in sources[0])
            {
                yield return item;
            }
            yield break;
        }
        
        // Concurrent merge using channels - reads from all sources in parallel
        // This enables proper concurrency scaling by allowing multiple producers to write simultaneously
        // Using capacity=1 to maintain backpressure behavior similar to sequential merge
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        
        // Start concurrent readers for all sources
        var readerTasks = sources.Select(source => ReadSourceIntoChannelAsync(source, channel.Writer)).ToList();
        
        // Start a completion task that completes the channel when all readers finish
        var completionTask = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(readerTasks);
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                channel.Writer.Complete(ex);
            }
        });
        
        // Yield items from the channel as they arrive
        await foreach (var item in channel.Reader.ReadAllAsync())
        {
            yield return item;
        }
        
        // Ensure completion task finishes and propagate any exceptions
        await completionTask;
    }
    
    /// <summary>
    /// Helper method to read from a source and write to a channel.
    /// Each source runs concurrently, enabling parallel reading from multiple upstream blocks.
    /// </summary>
    private static async Task ReadSourceIntoChannelAsync<T>(IAsyncEnumerable<T> source, ChannelWriter<T> writer)
    {
        await foreach (var item in source)
        {
            await writer.WriteAsync(item).ConfigureAwait(false);
        }
    }
    
    /// <summary>
    /// Enumerates a typed stream and routes items to typed routers without boxing.
    /// Equivalent to:
    ///   await foreach (var item in ((IAsyncEnumerable&lt;T&gt;)typedStream).WithCancellation(cancellationToken))
    ///   {
    ///       foreach (var router in routers)
    ///           await ((TypedEdgeRouter&lt;T&gt;)router).RouteTypedItemAsync(item, cancellationToken);
    ///   }
    /// </summary>
    /// <param name="typedStream">The typed stream to enumerate</param>
    /// <param name="itemType">The type of items in the stream</param>
    /// <param name="routers">The routers to route items through</param>
    /// <param name="epochStreamDelegate">Pre-compiled epoch stream routing delegate (if available from graph build time)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task EnumerateAndRouteTypedStreamAsync(
        object typedStream,
        Type itemType,
        List<ITypedEdgeRouter> routers,
        Func<object, List<ITypedEdgeRouter>, CancellationToken, Task>? epochStreamDelegate,
        CancellationToken cancellationToken)
    {
        // If we have a pre-compiled epoch stream delegate, use it directly
        // This path eliminates all type checking and reflection during execution
        if (epochStreamDelegate != null)
        {
            await epochStreamDelegate(typedStream, routers, cancellationToken);
            return;
        }
        
        // Fallback: compile the delegate at runtime (for backwards compatibility or buffer nodes)
        // Equivalent to: await EnumerateAndRouteTypedStreamGenericAsync<T>(typedStream, routers, cancellationToken);
        var method = typeof(ReflectionHelper).GetMethod(
            nameof(EnumerateAndRouteTypedStreamGenericAsync),
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
        {
            throw new InvalidOperationException($"Could not find method {nameof(EnumerateAndRouteTypedStreamGenericAsync)}");
        }
        
        var genericMethod = method.MakeGenericMethod(itemType);
        var task = (Task?)genericMethod.Invoke(null, new object[] { typedStream, routers, cancellationToken });
        
        if (task != null)
        {
            await task;
        }
    }
    
    private static async Task EnumerateAndRouteTypedStreamGenericAsync<T>(
        object typedStream,
        List<ITypedEdgeRouter> routers,
        CancellationToken cancellationToken)
    {
        // Check if T is IEpochStream<TItem> for some TItem
        if (IsEpochStreamType(typeof(T)))
        {
            // T is IEpochStream<TItem> - unwrap and route items, then re-wrap
            var itemType = GetEpochStreamItemType(typeof(T));
            
            // Get or create cached routing delegate for this item type
            // This avoids reflection overhead for every epoch stream
            var routingDelegate = _epochRoutingCache.GetOrAdd(itemType, type =>
            {
                // Build the delegate once using reflection
                var method = typeof(ReflectionHelper).GetMethod(
                    nameof(EnumerateAndRouteEpochStreamAsync),
                    BindingFlags.NonPublic | BindingFlags.Static);
                
                if (method == null)
                {
                    throw new InvalidOperationException($"Could not find method {nameof(EnumerateAndRouteEpochStreamAsync)}");
                }
                
                var genericMethod = method.MakeGenericMethod(type);
                
                // Create a compiled delegate that wraps the method invocation
                return (stream, rtrs, ct) => (Task)genericMethod.Invoke(null, new object[] { stream, rtrs, ct })!;
            });
            
            // Use the cached delegate to route the epoch stream
            await routingDelegate(typedStream, routers, cancellationToken);
            return;
        }
        
        // Standard routing for non-epoch stream types
        var stream = (IAsyncEnumerable<T>)typedStream;
        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            // Route to all routers concurrently - this enables parallel broadcast/routing
            // Each router writes to its channel(s) in parallel, avoiding serialization bottleneck
            if (routers.Count == 1)
            {
                // Optimization: single router doesn't need Task.WhenAll overhead
                var router = routers[0];
                if (router is TypedEdgeRouter<T> typedRouter)
                {
                    await typedRouter.RouteTypedItemAsync(item, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await router.RouteItemAsync(item!, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                // Multiple routers: route concurrently to avoid serialization
                var routingTasks = new Task[routers.Count];
                for (int i = 0; i < routers.Count; i++)
                {
                    var router = routers[i];
                    if (router is TypedEdgeRouter<T> typedRouter)
                    {
                        // Use internal method for zero-boxing routing
                        routingTasks[i] = typedRouter.RouteTypedItemAsync(item, cancellationToken);
                    }
                    else
                    {
                        // Fallback: box once and use public interface
                        routingTasks[i] = router.RouteItemAsync(item!, cancellationToken);
                    }
                }
                await Task.WhenAll(routingTasks).ConfigureAwait(false);
            }
        }
    }
    
    /// <summary>
    /// Enumerates a typed stream to completion (for terminal blocks).
    /// Equivalent to:
    ///   await foreach (var item in ((IAsyncEnumerable&lt;T&gt;)typedStream).WithCancellation(cancellationToken))
    ///   {
    ///       // Items are enumerated but not used
    ///   }
    /// </summary>
    public static async Task EnumerateTypedStreamAsync(
        object typedStream,
        Type itemType,
        CancellationToken cancellationToken)
    {
        // Equivalent to: await EnumerateTypedStreamGenericAsync<T>(typedStream, cancellationToken);
        var method = typeof(ReflectionHelper).GetMethod(
            nameof(EnumerateTypedStreamGenericAsync),
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
        {
            throw new InvalidOperationException($"Could not find method {nameof(EnumerateTypedStreamGenericAsync)}");
        }
        
        var genericMethod = method.MakeGenericMethod(itemType);
        var task = (Task?)genericMethod.Invoke(null, new object[] { typedStream, cancellationToken });
        
        if (task != null)
        {
            await task;
        }
    }
    
    private static async Task EnumerateTypedStreamGenericAsync<T>(
        object typedStream,
        CancellationToken cancellationToken)
    {
        var stream = (IAsyncEnumerable<T>)typedStream;
        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            // Enumerate to completion - terminal blocks
        }
    }
    
    /// <summary>
    /// Completes a typed channel writer, optionally with an exception.
    /// Equivalent to: ((ChannelWriter&lt;T&gt;)writerObj).Complete(exception);
    /// </summary>
    public static void CompleteTypedWriter(object writerObj, Exception? exception = null)
    {
        // Equivalent to: ((ChannelWriter<T>)writerObj).Complete(exception);
        var writerType = writerObj.GetType();
        var completeMethod = writerType.GetMethod("Complete");
        
        if (completeMethod == null)
        {
            throw new InvalidOperationException($"Could not find Complete method on {writerType.Name}");
        }
        
        completeMethod.Invoke(writerObj, new object?[] { exception });
    }

    /// <summary>
    /// Creates a factory function that instantiates typed router instances using reflection.
    /// This consolidates the expression-tree-based factory creation pattern used by router factories.
    /// </summary>
    /// <typeparam name="TRouter">The router interface type</typeparam>
    /// <param name="genericRouterType">The generic router type (e.g., typeof(TypedBufferNodeRouter&lt;&gt;))</param>
    /// <param name="itemType">The specific type parameter (e.g., int, string)</param>
    /// <param name="constructorParamTypes">The parameter types for the router constructor</param>
    /// <returns>A factory function that creates router instances</returns>
    public static Func<object, TRouter> CreateTypedRouterFactory<TRouter>(
        Type genericRouterType,
        Type itemType,
        Type[] constructorParamTypes)
    {
        var routerType = genericRouterType.MakeGenericType(itemType);
        var constructor = routerType.GetConstructor(constructorParamTypes)
            ?? throw new InvalidOperationException($"Could not find constructor for {routerType.Name}");

        // Build expression: (param) => new TypedRouter<T>(param)
        var param = Expression.Parameter(typeof(object), "param");
        var newExpr = Expression.New(constructor, param);
        var lambda = Expression.Lambda<Func<object, TRouter>>(newExpr, param);
        
        return lambda.Compile();
    }
    
    /// <summary>
    /// Enumerates epoch streams, unwraps them to route individual items,
    /// and re-wraps items into new epoch streams for each downstream consumer.
    /// This fixes the architectural mismatch where edges route containers instead of items.
    /// </summary>
    /// <typeparam name="TItem">The type of items within epoch streams</typeparam>
    private static async Task EnumerateAndRouteEpochStreamAsync<TItem>(
        object typedStream,
        List<ITypedEdgeRouter> routers,
        CancellationToken cancellationToken)
    {
        var stream = (IAsyncEnumerable<IEpochStream<TItem>>)typedStream;
        
        await foreach (var epochStream in stream.WithCancellation(cancellationToken))
        {
            // Step 1: Create downstream epoch streams for each router
            // Each router gets its own channel-backed epoch stream
            var downstreamStreams = CreateDownstreamEpochStreams(
                epochStream, routers);
            
            // Step 2: Route the epoch stream containers to downstream blocks
            // This happens before we start routing items
            await RouteEpochStreamContainersAsync(
                routers, downstreamStreams, cancellationToken);
            
            try
            {
                // Step 3: Route items from source epoch stream to downstream channels
                await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
                {
                    await RouteItemToDownstreamChannelsAsync(
                        item, routers, downstreamStreams, cancellationToken);
                }
                
                // Step 4: Complete all downstream channels (normal completion)
                // Use HashSet to avoid completing the same stream multiple times (for competing consumers)
                var completedStreams = new HashSet<ChannelBackedEpochStream<TItem>>();
                foreach (var (_, channelStream) in downstreamStreams)
                {
                    if (completedStreams.Add(channelStream))
                    {
                        channelStream.CompleteWriting();
                    }
                }
            }
            catch (Exception ex)
            {
                // Complete all downstream channels with error
                // Use HashSet to avoid completing the same stream multiple times (for competing consumers)
                var completedStreams = new HashSet<ChannelBackedEpochStream<TItem>>();
                foreach (var (_, channelStream) in downstreamStreams)
                {
                    if (completedStreams.Add(channelStream))
                    {
                        channelStream.CompleteWriting(ex);
                    }
                }
                throw;
            }
            finally
            {
                // Dispose the source epoch stream
                await epochStream.DisposeAsync();
            }
        }
    }
    
    /// <summary>
    /// Creates downstream epoch streams based on the edge strategy type.
    /// </summary>
    private static Dictionary<IBlock, ChannelBackedEpochStream<TItem>> 
        CreateDownstreamEpochStreams<TItem>(
            IEpochStream<TItem> sourceEpochStream,
            List<ITypedEdgeRouter> routers)
    {
        var downstreamStreams = new Dictionary<IBlock, ChannelBackedEpochStream<TItem>>();
        
        foreach (var router in routers)
        {
            var strategy = router.Strategy;
            
            if (strategy.EdgeType == EdgeType.Competing)
            {
                CreateCompetingConsumerStreams(sourceEpochStream, router, downstreamStreams);
            }
            else // Broadcast or Routed
            {
                CreateBroadcastOrRoutedStreams(sourceEpochStream, router, downstreamStreams);
            }
        }
        
        return downstreamStreams;
    }
    
    /// <summary>
    /// Creates a single shared channel-backed epoch stream for competing consumers.
    /// All targets share ONE channel-backed epoch stream with a shared backing channel.
    /// </summary>
    private static void CreateCompetingConsumerStreams<TItem>(
        IEpochStream<TItem> sourceEpochStream,
        ITypedEdgeRouter router,
        Dictionary<IBlock, ChannelBackedEpochStream<TItem>> downstreamStreams)
    {
        var bufferCapacity = router.Strategy.BufferCapacity;
        var sharedChannel = Channel.CreateBounded<TItem>(new BoundedChannelOptions(bufferCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false, // Multiple consumers compete for items
            SingleWriter = true   // Single source enumeration writes items
        });
        
        var sharedStream = new ChannelBackedEpochStream<TItem>(
            sourceEpochStream.Epoch,
            sourceEpochStream.EpochScope,
            sharedChannel);
        
        // All target blocks share the same stream instance
        foreach (var targetBlock in router.TargetBlocks)
        {
            downstreamStreams[targetBlock] = sharedStream;
        }
    }
    
    /// <summary>
    /// Creates unique channel-backed epoch streams for broadcast or routed topologies.
    /// Each target gets its own channel-backed epoch stream.
    /// </summary>
    private static void CreateBroadcastOrRoutedStreams<TItem>(
        IEpochStream<TItem> sourceEpochStream,
        ITypedEdgeRouter router,
        Dictionary<IBlock, ChannelBackedEpochStream<TItem>> downstreamStreams)
    {
        var bufferCapacity = router.Strategy.BufferCapacity;
        
        foreach (var targetBlock in router.TargetBlocks)
        {
            var channel = Channel.CreateBounded<TItem>(new BoundedChannelOptions(bufferCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,  // Each target is the sole reader of its channel
                SingleWriter = true   // Single source enumeration writes items
            });
            
            var channelBackedStream = new ChannelBackedEpochStream<TItem>(
                sourceEpochStream.Epoch,
                sourceEpochStream.EpochScope,
                channel);
            
            downstreamStreams[targetBlock] = channelBackedStream;
        }
    }
    
    /// <summary>
    /// Routes epoch stream containers to downstream blocks.
    /// Strategy-aware routing:
    /// - Broadcast: Each target gets its own unique container
    /// - Competing: All targets get the same shared container
    /// - Routed: Each target gets its own unique container
    /// Uses pre-compiled delegates to eliminate dynamic casts and type checking.
    /// </summary>
    private static async Task RouteEpochStreamContainersAsync<TItem>(
        List<ITypedEdgeRouter> routers,
        Dictionary<IBlock, ChannelBackedEpochStream<TItem>> downstreamStreams,
        CancellationToken cancellationToken)
    {
        // Get the pre-compiled container routing delegate for this item type
        // This eliminates dynamic casts and type checks for every router
        var routingDelegate = _containerRoutingCache.TryGetValue(typeof(TItem), out var cached)
            ? cached
            : null;
        
        // Route containers to target blocks
        foreach (var router in routers)
        {
            if (routingDelegate != null)
            {
                // Use pre-compiled delegate (zero overhead path)
                if (router.Strategy.EdgeType == EdgeType.Competing)
                {
                    // Competing: All targets share the same container
                    // Route the shared container once to all targets
                    if (router.TargetBlocks.Count > 0)
                    {
                        var sharedContainer = downstreamStreams[router.TargetBlocks[0]];
                        foreach (var targetBlock in router.TargetBlocks)
                        {
                            await routingDelegate(router, sharedContainer, targetBlock, cancellationToken);
                        }
                    }
                }
                else
                {
                    // Broadcast/Routed: Each target gets its own unique container
                    foreach (var targetBlock in router.TargetBlocks)
                    {
                        var container = downstreamStreams[targetBlock];
                        await routingDelegate(router, container, targetBlock, cancellationToken);
                    }
                }
            }
            else
            {
                // Fallback: use dynamic cast (only if delegate not available)
                var routerType = router.GetType();
                var expectedType = typeof(TypedEdgeRouter<>).MakeGenericType(typeof(IEpochStream<TItem>));
                
                if (routerType == expectedType)
                {
                    var typedRouter = router as dynamic;
                    
                    if (router.Strategy.EdgeType == EdgeType.Competing)
                    {
                        // Competing: All targets share the same container
                        // Route the shared container once to all targets
                        if (router.TargetBlocks.Count > 0)
                        {
                            var sharedContainer = downstreamStreams[router.TargetBlocks[0]];
                            foreach (var targetBlock in router.TargetBlocks)
                            {
                                await typedRouter.RouteToSpecificTargetAsync(sharedContainer, targetBlock, cancellationToken);
                            }
                        }
                    }
                    else
                    {
                        // Broadcast/Routed: Each target gets its own unique container
                        foreach (var targetBlock in router.TargetBlocks)
                        {
                            var container = downstreamStreams[targetBlock];
                            await typedRouter.RouteToSpecificTargetAsync(container, targetBlock, cancellationToken);
                        }
                    }
                }
                else
                {
                    // Fallback for non-TypedEdgeRouter routers
                    foreach (var targetBlock in router.TargetBlocks)
                    {
                        var container = downstreamStreams[targetBlock];
                        await router.RouteItemAsync(container, cancellationToken);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Routes an individual item to downstream epoch stream channels.
    /// Strategy-specific routing logic:
    /// - Broadcast: Write to all target channels concurrently
    /// - Competing: Write to one shared channel (all targets have same stream instance)
    /// - Routed/Selective: Apply routing logic to determine target channel(s)
    /// </summary>
    private static async Task RouteItemToDownstreamChannelsAsync<TItem>(
        TItem item,
        List<ITypedEdgeRouter> routers,
        Dictionary<IBlock, ChannelBackedEpochStream<TItem>> downstreamStreams,
        CancellationToken cancellationToken)
    {
        // Strategy-aware routing
        var writeTasks = new List<Task>();
        var processedStreams = new HashSet<ChannelBackedEpochStream<TItem>>(); // Track to avoid duplicate writes
        
        foreach (var router in routers)
        {
            var strategy = router.Strategy;
            
            if (strategy.EdgeType == EdgeType.Competing)
            {
                // Competing: Write to shared channel once (all targets have same stream)
                if (router.TargetBlocks.Count > 0)
                {
                    var sharedStream = downstreamStreams[router.TargetBlocks[0]];
                    if (!processedStreams.Contains(sharedStream))
                    {
                        writeTasks.Add(sharedStream.GetWriter().WriteAsync(item, cancellationToken).AsTask());
                        processedStreams.Add(sharedStream);
                    }
                }
            }
            else if (strategy.EdgeType == EdgeType.Broadcast)
            {
                // Broadcast: Write to all target channels concurrently
                foreach (var targetBlock in router.TargetBlocks)
                {
                    var stream = downstreamStreams[targetBlock];
                    if (!processedStreams.Contains(stream))
                    {
                        writeTasks.Add(stream.GetWriter().WriteAsync(item, cancellationToken).AsTask());
                        processedStreams.Add(stream);
                    }
                }
            }
            else if (strategy.EdgeType == EdgeType.Routed)
            {
                // Routed/Selective: Use strategy's routing logic to determine target
                // For selective routing, apply the route selector to each item
                
                if (strategy is SelectiveRoutingEdgeStrategy<TItem> selectiveStrategy)
                {
                    // Use the strategy's public method to evaluate the route key
                    var routeKey = selectiveStrategy.EvaluateRouteKey(item);
                    var routeKeyToBlock = selectiveStrategy.RouteKeyToBlock;
                    
                    if (routeKeyToBlock.TryGetValue(routeKey, out var targetBlock))
                    {
                        var stream = downstreamStreams[targetBlock];
                        if (!processedStreams.Contains(stream))
                        {
                            writeTasks.Add(stream.GetWriter().WriteAsync(item, cancellationToken).AsTask());
                            processedStreams.Add(stream);
                        }
                    }
                    // If route not found, item is dropped (matches strategy behavior)
                }
                else
                {
                    // For other routed strategies, write to all targets (fallback)
                    foreach (var targetBlock in router.TargetBlocks)
                    {
                        var stream = downstreamStreams[targetBlock];
                        if (!processedStreams.Contains(stream))
                        {
                            writeTasks.Add(stream.GetWriter().WriteAsync(item, cancellationToken).AsTask());
                            processedStreams.Add(stream);
                        }
                    }
                }
            }
        }
        
        if (writeTasks.Count == 1)
        {
            // Optimization: single write doesn't need Task.WhenAll
            await writeTasks[0].ConfigureAwait(false);
        }
        else if (writeTasks.Count > 1)
        {
            // Multiple writes: execute concurrently
            await Task.WhenAll(writeTasks).ConfigureAwait(false);
        }
    }
}
