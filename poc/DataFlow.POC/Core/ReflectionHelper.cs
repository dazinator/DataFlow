namespace DataFlow.POC.Core;

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
}
