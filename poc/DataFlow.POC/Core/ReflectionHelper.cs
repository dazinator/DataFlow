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
    public static async Task EnumerateAndRouteTypedStreamAsync(
        object typedStream,
        Type itemType,
        List<ITypedEdgeRouter> routers,
        CancellationToken cancellationToken)
    {
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
            var method = typeof(ReflectionHelper).GetMethod(
                nameof(EnumerateAndRouteEpochStreamAsync),
                BindingFlags.NonPublic | BindingFlags.Static);
            
            if (method == null)
            {
                throw new InvalidOperationException($"Could not find method {nameof(EnumerateAndRouteEpochStreamAsync)}");
            }
            
            var genericMethod = method.MakeGenericMethod(itemType);
            var task = (Task?)genericMethod.Invoke(null, new object[] { typedStream, routers, cancellationToken });
            
            if (task != null)
            {
                await task;
            }
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
                foreach (var (_, channelStream) in downstreamStreams)
                {
                    channelStream.CompleteWriting();
                }
            }
            catch (Exception ex)
            {
                // Complete all downstream channels with error
                foreach (var (_, channelStream) in downstreamStreams)
                {
                    channelStream.CompleteWriting(ex);
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
    /// Creates downstream epoch streams for each router.
    /// Each router gets a channel-backed epoch stream with the same metadata.
    /// </summary>
    private static Dictionary<ITypedEdgeRouter, ChannelBackedEpochStream<TItem>> 
        CreateDownstreamEpochStreams<TItem>(
            IEpochStream<TItem> sourceEpochStream,
            List<ITypedEdgeRouter> routers)
    {
        var downstreamStreams = new Dictionary<ITypedEdgeRouter, ChannelBackedEpochStream<TItem>>();
        
        // Create one channel per router (broadcast-style for now)
        // Strategy-specific logic will be added in Phase 2
        foreach (var router in routers)
        {
            // Create bounded channel with configurable capacity
            // Use same capacity as edge strategy (default 100)
            var channel = Channel.CreateBounded<TItem>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
            
            var channelBackedStream = new ChannelBackedEpochStream<TItem>(
                sourceEpochStream.Epoch,
                sourceEpochStream.EpochScope,
                channel);
            
            downstreamStreams[router] = channelBackedStream;
        }
        
        return downstreamStreams;
    }
    
    /// <summary>
    /// Routes epoch stream containers to downstream blocks via their routers.
    /// Each router receives its corresponding channel-backed epoch stream.
    /// </summary>
    private static async Task RouteEpochStreamContainersAsync<TItem>(
        List<ITypedEdgeRouter> routers,
        Dictionary<ITypedEdgeRouter, ChannelBackedEpochStream<TItem>> downstreamStreams,
        CancellationToken cancellationToken)
    {
        // For each router, route the epoch stream container
        // The router is of type TypedEdgeRouter<IEpochStream<TItem>>
        // We need to route IEpochStream<TItem> objects through it
        
        foreach (var router in routers)
        {
            var epochStreamContainer = downstreamStreams[router];
            // Route the epoch stream container via the router's strategy
            // This writes the IEpochStream<TItem> to the downstream block's input channel
            await router.RouteItemAsync(epochStreamContainer, cancellationToken);
        }
    }
    
    /// <summary>
    /// Routes an individual item to downstream epoch stream channels.
    /// Items are written directly to the backing channels, not through routers.
    /// </summary>
    private static async Task RouteItemToDownstreamChannelsAsync<TItem>(
        TItem item,
        List<ITypedEdgeRouter> routers,
        Dictionary<ITypedEdgeRouter, ChannelBackedEpochStream<TItem>> downstreamStreams,
        CancellationToken cancellationToken)
    {
        if (routers.Count == 1)
        {
            // Optimization: single router doesn't need Task.WhenAll
            var router = routers[0];
            var stream = downstreamStreams[router];
            await stream.GetWriter().WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Multiple routers: write concurrently (broadcast semantics for now)
            var writeTasks = new Task[routers.Count];
            for (int i = 0; i < routers.Count; i++)
            {
                var router = routers[i];
                var stream = downstreamStreams[router];
                writeTasks[i] = stream.GetWriter().WriteAsync(item, cancellationToken).AsTask();
            }
            await Task.WhenAll(writeTasks).ConfigureAwait(false);
        }
    }
}
