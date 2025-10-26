namespace DataFlow.POC.Core;

/// <summary>
/// Represents a first-class buffer node in the dataflow graph.
/// A buffer node is backed by a Channel&lt;T&gt; and serves as a connection point
/// between multiple producers and multiple consumers.
/// 
/// Unlike IBlock, BufferNode has no transformation logic - it's purely a buffering
/// and routing point for fan-in (multiple producers) and fan-out (multiple consumers) scenarios.
/// </summary>
public class BufferNode
{
    /// <summary>
    /// Creates a new buffer node with the specified data type and capacity.
    /// </summary>
    /// <param name="dataType">The type of data flowing through this buffer</param>
    /// <param name="capacity">The maximum capacity of the backing channel (for backpressure control)</param>
    /// <param name="name">Optional name for debugging and logging purposes</param>
    public BufferNode(Type dataType, int capacity, string? name = null)
    {
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        Capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0");
        Name = name;
    }

    /// <summary>
    /// Optional name for this buffer node (for debugging and logging).
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// The type of data flowing through this buffer.
    /// </summary>
    public Type DataType { get; }

    /// <summary>
    /// The maximum capacity of the backing channel.
    /// This controls backpressure - when the buffer is full, upstream producers will wait.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets the display name for this buffer node.
    /// Returns the name if set, otherwise returns "&lt;unnamed&gt;".
    /// </summary>
    public string GetName() => !string.IsNullOrEmpty(Name) ? Name : "<unnamed>";

    public override string ToString()
    {
        var nameStr = GetName();
        return $"BufferNode({nameStr}, {DataType.Name}, Capacity={Capacity})";
    }
}

/// <summary>
/// Generic version of BufferNode for compile-time type safety.
/// </summary>
public class BufferNode<T> : BufferNode
{
    public BufferNode(int capacity, string? name = null)
        : base(typeof(T), capacity, name)
    {
    }
}
