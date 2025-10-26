namespace DataFlow.POC.Core;

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
    
    private static async IAsyncEnumerable<T> MergeAsyncEnumerables<T>(List<IAsyncEnumerable<T>> sources)
    {
        // Simple merge - interleaves items from multiple sources
        // Note: More sophisticated merge strategies could be implemented
        var enumerators = sources.Select(s => s.GetAsyncEnumerator()).ToList();
        try
        {
            while (true)
            {
                var hasAny = false;
                foreach (var enumerator in enumerators)
                {
                    if (await enumerator.MoveNextAsync())
                    {
                        hasAny = true;
                        yield return enumerator.Current;
                    }
                }
                if (!hasAny) break;
            }
        }
        finally
        {
            foreach (var enumerator in enumerators)
            {
                await enumerator.DisposeAsync();
            }
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
            // Route to all routers - using internal typed method when available for zero-boxing
            foreach (var router in routers)
            {
                if (router is TypedEdgeRouter<T> typedRouter)
                {
                    // Use internal method for zero-boxing routing
                    await typedRouter.RouteTypedItemAsync(item, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Fallback: box once and use public interface
                    await router.RouteItemAsync(item!, cancellationToken).ConfigureAwait(false);
                }
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
}
