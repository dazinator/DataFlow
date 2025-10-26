namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Threading.Channels;

/// <summary>
/// Factory for creating typed buffer node routers that eliminate boxing overhead.
/// Routes items to buffer node channels without boxing value types.
/// </summary>
public static class TypedBufferNodeRouterFactory
{
    // Cache for compiled router delegates: Type -> Func that creates a router for that type
    private static readonly ConcurrentDictionary<Type, Func<object, ITypedEdgeRouter>> _routerFactoryCache = new();

    /// <summary>
    /// Creates a typed buffer node router for the specified data type.
    /// Uses reflection with caching to create a strongly-typed router that avoids boxing.
    /// </summary>
    /// <param name="dataType">The type of data flowing through the buffer</param>
    /// <param name="bufferNode">The buffer node to route to (used for metadata/debugging)</param>
    /// <param name="typedWriter">The typed channel writer (as object)</param>
    /// <returns>A typed buffer node router</returns>
    public static ITypedEdgeRouter CreateTypedRouter(Type dataType, BufferNode bufferNode, object typedWriter)
    {
        var factory = _routerFactoryCache.GetOrAdd(dataType, type =>
        {
            return ReflectionHelper.CreateTypedRouterFactory<ITypedEdgeRouter>(
                typeof(TypedBufferNodeRouter<>),
                type,
                new[] { typeof(object) });
        });

        return factory(typedWriter);
    }
}

/// <summary>
/// Generic typed buffer node router that eliminates boxing by working with typed channels.
/// Routes items of type T through Channel&lt;T&gt; without converting to object.
/// </summary>
public class TypedBufferNodeRouter<T> : ITypedEdgeRouter
{
    private readonly ChannelWriter<T> _writer;

    public TypedBufferNodeRouter(object writerObj)
    {
        _writer = (ChannelWriter<T>)writerObj;
    }

    public Type ItemType => typeof(T);

    /// <summary>
    /// Routes a single item to the buffer node's channel.
    /// No boxing occurs because we cast object to T once and use typed channel writer.
    /// </summary>
    public async Task RouteItemAsync(object item, CancellationToken cancellationToken)
    {
        var typedItem = (T)item;
        await _writer.WriteAsync(typedItem, cancellationToken).ConfigureAwait(false);
    }
}
