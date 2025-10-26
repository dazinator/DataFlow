namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;

/// <summary>
/// Factory for creating typed channels using reflection with caching.
/// This eliminates boxing overhead for value types while maintaining flexibility.
/// Returns typed Channel<T> writers and readers directly.
/// </summary>
public static class TypedChannelFactory
{
    // Cache for channel creation methods: Channel.CreateBounded<T> or Channel.CreateUnbounded<T>
    private static readonly ConcurrentDictionary<(Type dataType, BufferMode mode, int capacity), MethodInfo> _cachedCreateMethods = new();
    
    /// <summary>
    /// Creates a typed channel for the specified data type and buffer configuration.
    /// Uses reflection with caching to create Channel&lt;T&gt; instances at runtime.
    /// Returns typed writer and reader objects that can be cast to ChannelWriter&lt;T&gt; and ChannelReader&lt;T&gt;.
    /// </summary>
    /// <param name="dataType">The type of data flowing through the channel</param>
    /// <param name="bufferMode">The buffering mode (None or Bounded)</param>
    /// <param name="bufferCapacity">The capacity for bounded channels</param>
    /// <param name="singleReader">Whether the channel has a single reader</param>
    /// <param name="singleWriter">Whether the channel has a single writer</param>
    /// <returns>A tuple containing the typed writer and reader objects</returns>
    public static (object writer, object reader) CreateTypedChannel(
        Type dataType,
        BufferMode bufferMode,
        int bufferCapacity,
        bool singleReader,
        bool singleWriter)
    {
        var key = (dataType, bufferMode, bufferCapacity);
        
        var createMethod = _cachedCreateMethods.GetOrAdd(key, _ =>
        {
            // Looking for: Channel.CreateBounded<T>(BoundedChannelOptions) or Channel.CreateUnbounded<T>(UnboundedChannelOptions)
            MethodInfo? method = bufferMode == BufferMode.Bounded
                ? typeof(Channel).GetMethod(nameof(Channel.CreateBounded), new[] { typeof(BoundedChannelOptions) })
                : typeof(Channel).GetMethod(nameof(Channel.CreateUnbounded), new[] { typeof(UnboundedChannelOptions) });
            
            if (method == null)
            {
                throw new InvalidOperationException($"Could not find Channel.Create method for mode {bufferMode}");
            }
            
            // Make it generic with the data type
            return method.MakeGenericMethod(dataType);
        });
        
        // Create channel options
        object options = bufferMode == BufferMode.Bounded
            ? new BoundedChannelOptions(bufferCapacity)
            {
                SingleReader = singleReader,
                SingleWriter = singleWriter,
                FullMode = BoundedChannelFullMode.Wait
            }
            : (object)new UnboundedChannelOptions
            {
                SingleReader = singleReader,
                SingleWriter = singleWriter
            };
        
        // Invoke the Create method: var channel = Channel.CreateBounded<T>(options);
        var channel = createMethod.Invoke(null, new[] { options });
        
        if (channel == null)
        {
            throw new InvalidOperationException($"Failed to create channel for type {dataType.Name}");
        }
        
        // Get writer and reader properties: channel.Writer and channel.Reader
        var channelType = channel.GetType();
        var writerProperty = channelType.GetProperty("Writer") 
            ?? throw new InvalidOperationException("Could not find Writer property on channel");
        var readerProperty = channelType.GetProperty("Reader") 
            ?? throw new InvalidOperationException("Could not find Reader property on channel");
        
        var writer = writerProperty.GetValue(channel) 
            ?? throw new InvalidOperationException("Failed to get Writer from channel");
        var reader = readerProperty.GetValue(channel) 
            ?? throw new InvalidOperationException("Failed to get Reader from channel");
        
        // Return typed writer and reader directly (they are ChannelWriter<T> and ChannelReader<T>)
        return (writer, reader);
    }
}
