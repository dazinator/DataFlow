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
    /// Cache for compiled container routing delegates to avoid repeated reflection.
    /// Key is the item type (TItem), value is the compiled delegate that can call RouteToSpecificTargetAsync.
    /// Populated at graph build time by CreateContainerRoutingDelegate.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<ITypedEdgeRouter, object, IBlock, CancellationToken, Task>>
        _containerRoutingCache = new();

    /// <summary>
    /// Cache for input-counter wrapping delegates, keyed by item type.
    /// Built once on first use; subsequent calls are a dictionary lookup + delegate invoke.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<object, long[], object>>
        _inputCounterWrapperCache = new();
    
    /// <summary>
    /// Returns a pre-compiled container routing delegate for the given item type, if available.
    /// Populated at graph build time by CreateContainerRoutingDelegate.
    /// </summary>
    internal static Func<ITypedEdgeRouter, object, IBlock, CancellationToken, Task>? TryGetContainerRoutingDelegate(Type itemType)
        => _containerRoutingCache.TryGetValue(itemType, out var cached) ? cached : null;

    /// <summary>
    /// Determines if a type is IEpochStream&lt;T&gt; for some T.
    /// Uses caching to minimize reflection overhead.
    /// </summary>
    internal static bool IsEpochStreamType(Type type)
    {
        return _isEpochStreamTypeCache.GetOrAdd(type, t =>
            t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEpochStream<>));
    }
    
    /// <summary>
    /// Extracts the item type from IEpochStream&lt;T&gt;.
    /// </summary>
    internal static Type GetEpochStreamItemType(Type epochStreamType)
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
    public static Func<object, List<ITypedEdgeRouter>, CancellationToken, Task<long>>? CreateEpochStreamRoutingDelegate(Type edgeDataType)
    {
        if (!IsEpochStreamType(edgeDataType))
        {
            return null;
        }
        
        var itemType = GetEpochStreamItemType(edgeDataType);

        return StreamPump.CreateEpochRoutingDelegate(itemType);
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
    /// Wraps <paramref name="typedInput"/> (an <c>IAsyncEnumerable&lt;T&gt;</c> boxed as object)
    /// with a thin counting shim that atomically increments <paramref name="inputCounter"/>[0]
    /// for every item yielded. Returns <see langword="null"/> when <paramref name="typedInput"/>
    /// is <see langword="null"/> (source blocks have no input).
    ///
    /// The wrapping delegate is compiled once per item type and cached; the hot-path cost per
    /// item is a single <see cref="System.Threading.Interlocked.Increment"/> call.
    /// </summary>
    internal static object? WrapWithInputCounter(object? typedInput, Type inputItemType, long[] inputCounter)
    {
        if (typedInput == null) return null;

        var wrapper = _inputCounterWrapperCache.GetOrAdd(inputItemType, static type =>
        {
            var method = typeof(StreamPump).GetMethod(
                nameof(StreamPump.CreateCountingAsyncEnumerable),
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)
                ?? throw new InvalidOperationException(
                    $"Could not find method {nameof(StreamPump.CreateCountingAsyncEnumerable)}");

            var genericMethod = method.MakeGenericMethod(type);
            return (input, counter) => genericMethod.Invoke(null, new object[] { input, counter })!;
        });

        return wrapper(typedInput, inputCounter);
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
        return StreamPump.MergeAsyncEnumerables(typed);
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
    public static async Task<long> EnumerateAndRouteTypedStreamAsync(
        object typedStream,
        Type itemType,
        List<ITypedEdgeRouter> routers,
        Func<object, List<ITypedEdgeRouter>, CancellationToken, Task<long>>? epochStreamDelegate,
        long[]? progressCounter,
        CancellationToken cancellationToken)
    {
        // If we have a pre-compiled epoch stream delegate, use it directly.
        // Note: epoch stream delegates have a fixed signature and cannot thread progressCounter,
        // so live 500ms ticks are not emitted for epoch-stream edges.
        if (epochStreamDelegate != null)
        {
            return await epochStreamDelegate(typedStream, routers, cancellationToken);
        }

        // Fallback: compile the delegate at runtime (for backwards compatibility or buffer nodes)
        // Equivalent to: await StreamPump.EnumerateAndRouteTypedStreamGenericAsync<T>(typedStream, routers, progressCounter, cancellationToken);
        var method = typeof(StreamPump).GetMethod(
            nameof(StreamPump.EnumerateAndRouteTypedStreamGenericAsync),
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);

        if (method == null)
        {
            throw new InvalidOperationException($"Could not find method {nameof(StreamPump.EnumerateAndRouteTypedStreamGenericAsync)}");
        }

        var genericMethod = method.MakeGenericMethod(itemType);
        var task = (Task<long>?)genericMethod.Invoke(null, new object[] { typedStream, routers, progressCounter, cancellationToken });

        return task != null ? await task : 0L;
    }
    
    /// <summary>
    /// Enumerates a typed stream to completion (for terminal blocks).
    /// Equivalent to:
    ///   await foreach (var item in ((IAsyncEnumerable&lt;T&gt;)typedStream).WithCancellation(cancellationToken))
    ///   {
    ///       // Items are enumerated but not used
    ///   }
    /// </summary>
    public static async Task<long> EnumerateTypedStreamAsync(
        object typedStream,
        Type itemType,
        long[]? progressCounter,
        CancellationToken cancellationToken)
    {
        // Equivalent to: await StreamPump.EnumerateTypedStreamGenericAsync<T>(typedStream, progressCounter, cancellationToken);
        var method = typeof(StreamPump).GetMethod(
            nameof(StreamPump.EnumerateTypedStreamGenericAsync),
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);

        if (method == null)
        {
            throw new InvalidOperationException($"Could not find method {nameof(StreamPump.EnumerateTypedStreamGenericAsync)}");
        }

        var genericMethod = method.MakeGenericMethod(itemType);
        var task = (Task<long>?)genericMethod.Invoke(null, new object[] { typedStream, progressCounter, cancellationToken });

        return task != null ? await task : 0L;
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
    /// <param name="genericRouterType">The generic router type (e.g., typeof(TypedEdgeRouter&lt;&gt;))</param>
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
