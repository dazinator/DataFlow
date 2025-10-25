namespace DataFlow.POC.Core;

using System.Reflection;
using System.Threading.Channels;

/// <summary>
/// An adapter that wraps a typed channel and provides non-generic methods to interact with it.
/// All reflection is performed once during instantiation and cached for the lifetime of the adapter.
/// This provides optimal performance for hot-path operations like WriteAsync.
/// </summary>
public sealed class TypedChannelAdapter
{
    private readonly object _writer;
    private readonly object _reader;
    private readonly Type _itemType;
    
    // Cached method info and delegates - set once during construction
    private readonly Func<object, CancellationToken, ValueTask> _writeAsyncDelegate;
    private readonly MethodInfo _completeMethod;
    private readonly MethodInfo _readAllAsyncMethod;
    
    /// <summary>
    /// Creates a new TypedChannelAdapter wrapping a typed channel.
    /// All reflection work is performed during construction and cached.
    /// </summary>
    /// <param name="writer">The typed ChannelWriter&lt;T&gt; instance</param>
    /// <param name="reader">The typed ChannelReader&lt;T&gt; instance</param>
    /// <param name="itemType">The type T of items flowing through the channel</param>
    public TypedChannelAdapter(object writer, object reader, Type itemType)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _itemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
        
        // Cache WriteAsync delegate - this is the hot path
        // Looking for: ChannelWriter<T>.WriteAsync(T item, CancellationToken ct)
        var writerType = typeof(ChannelWriter<>).MakeGenericType(itemType);
        var writeMethod = writerType.GetMethod(nameof(ChannelWriter<int>.WriteAsync), new[] { itemType, typeof(CancellationToken) })
            ?? throw new InvalidOperationException($"Could not find WriteAsync method on {writerType.Name}");
        
        // Create a delegate that captures the method info for efficient invocation
        _writeAsyncDelegate = (item, ct) =>
        {
            // Invoke: writer.WriteAsync(item, ct)
            var valueTask = writeMethod.Invoke(_writer, new[] { item, ct });
            
            if (valueTask == null)
            {
                return default;
            }
            
            // Convert ValueTask<T> to ValueTask
            var asTaskMethod = valueTask.GetType().GetMethod(nameof(ValueTask.AsTask));
            if (asTaskMethod != null)
            {
                var task = asTaskMethod.Invoke(valueTask, null) as Task;
                if (task != null)
                {
                    return new ValueTask(task);
                }
            }
            
            return default;
        };
        
        // Cache Complete method info
        // Looking for: ChannelWriter<T>.Complete(Exception? exception)
        _completeMethod = writer.GetType().GetMethod(nameof(ChannelWriter<int>.Complete), new[] { typeof(Exception) })
            ?? throw new InvalidOperationException($"Could not find Complete method on {writer.GetType().Name}");
        
        // Cache ReadAllAsync method info
        // Looking for: ChannelReader<T>.ReadAllAsync(CancellationToken ct)
        var readerType = typeof(ChannelReader<>).MakeGenericType(itemType);
        _readAllAsyncMethod = readerType.GetMethod(nameof(ChannelReader<int>.ReadAllAsync))
            ?? throw new InvalidOperationException($"Could not find ReadAllAsync method on {readerType.Name}");
    }
    
    /// <summary>
    /// Gets the underlying writer object (ChannelWriter&lt;T&gt;).
    /// </summary>
    public object Writer => _writer;
    
    /// <summary>
    /// Gets the underlying reader object (ChannelReader&lt;T&gt;).
    /// </summary>
    public object Reader => _reader;
    
    /// <summary>
    /// Gets the type of items flowing through the channel.
    /// </summary>
    public Type ItemType => _itemType;
    
    /// <summary>
    /// Writes an item to the channel using the cached delegate.
    /// This is optimized for hot-path performance with no reflection overhead per call.
    /// </summary>
    public async ValueTask WriteAsync(object item, CancellationToken cancellationToken = default)
    {
        await _writeAsyncDelegate(item, cancellationToken);
    }
    
    /// <summary>
    /// Completes the channel writer using cached method info.
    /// </summary>
    public void Complete(Exception? exception = null)
    {
        _completeMethod.Invoke(_writer, new object?[] { exception });
    }
    
    /// <summary>
    /// Reads all items from the channel as an async enumerable.
    /// Uses cached method info with minimal reflection overhead.
    /// </summary>
    public async IAsyncEnumerable<object> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Invoke: reader.ReadAllAsync(ct)
        var asyncEnumerable = _readAllAsyncMethod.Invoke(_reader, new object[] { cancellationToken });
        
        if (asyncEnumerable == null)
        {
            throw new InvalidOperationException("ReadAllAsync returned null");
        }
        
        // Get enumerator - these lookups could be cached too, but they're only done once per ReadAllAsync call
        var asyncEnumerableType = typeof(IAsyncEnumerable<>).MakeGenericType(_itemType);
        var getEnumeratorMethod = asyncEnumerableType.GetMethod(nameof(IAsyncEnumerable<object>.GetAsyncEnumerator))
            ?? throw new InvalidOperationException($"Could not find GetAsyncEnumerator method on {asyncEnumerableType.Name}");
        
        var enumerator = getEnumeratorMethod.Invoke(asyncEnumerable, new object[] { cancellationToken });
        if (enumerator == null)
        {
            throw new InvalidOperationException("GetAsyncEnumerator returned null");
        }
        
        var enumeratorType = typeof(IAsyncEnumerator<>).MakeGenericType(_itemType);
        var moveNextMethod = enumeratorType.GetMethod(nameof(IAsyncEnumerator<object>.MoveNextAsync))
            ?? throw new InvalidOperationException($"Could not find MoveNextAsync on {enumeratorType.Name}");
        var currentProperty = enumeratorType.GetProperty(nameof(IAsyncEnumerator<object>.Current))
            ?? throw new InvalidOperationException($"Could not find Current on {enumeratorType.Name}");
        
        try
        {
            while (true)
            {
                var moveNextValueTask = moveNextMethod.Invoke(enumerator, Array.Empty<object>());
                if (moveNextValueTask == null)
                {
                    break;
                }
                
                // Convert ValueTask<bool> to Task<bool> and await it
                var asTaskMethod = moveNextValueTask.GetType().GetMethod(nameof(ValueTask<bool>.AsTask));
                if (asTaskMethod == null)
                {
                    break;
                }
                
                var task = asTaskMethod.Invoke(moveNextValueTask, Array.Empty<object>()) as Task<bool>;
                if (task == null)
                {
                    break;
                }
                
                var hasNext = await task;
                if (!hasNext)
                {
                    break;
                }
                
                var current = currentProperty.GetValue(enumerator);
                if (current != null)
                {
                    yield return current;
                }
            }
        }
        finally
        {
            // Dispose the enumerator
            if (enumerator is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }
    }
}
