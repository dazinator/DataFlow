namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Threading.Channels;

/// <summary>
/// Creates IBufferMonitor instances from typed channel readers stored as object.
/// Uses compiled expression trees (cached per type) so Count is read with a direct
/// virtual call — no reflection overhead on each timer tick.
/// </summary>
public static class BufferMonitorFactory
{
    // Cached delegates: Type -> (object readerObj) -> int count
    private static readonly ConcurrentDictionary<Type, Func<object, int>> _countGetters = new();

    /// <summary>
    /// Creates a monitor for one edge channel.
    /// </summary>
    /// <param name="sourceBlock">Source block name.</param>
    /// <param name="targetBlock">Target block name.</param>
    /// <param name="capacity">Channel capacity; 0 for unbounded.</param>
    /// <param name="itemType">The T in ChannelReader&lt;T&gt;.</param>
    /// <param name="readerObj">The ChannelReader&lt;T&gt; instance stored as object.</param>
    public static IBufferMonitor Create(
        string sourceBlock,
        string targetBlock,
        int capacity,
        Type itemType,
        object readerObj)
    {
        var getter = _countGetters.GetOrAdd(itemType, BuildCountGetter);
        return new BufferMonitor(sourceBlock, targetBlock, capacity, () => getter(readerObj));
    }

    private static Func<object, int> BuildCountGetter(Type itemType)
    {
        // Compiles: (object reader) => ((ChannelReader<T>)reader).Count
        var readerType = typeof(ChannelReader<>).MakeGenericType(itemType);
        var param = Expression.Parameter(typeof(object), "reader");
        var cast = Expression.Convert(param, readerType);
        var countProp = Expression.Property(cast, nameof(ChannelReader<object>.Count));
        return Expression.Lambda<Func<object, int>>(countProp, param).Compile();
    }

    private sealed class BufferMonitor : IBufferMonitor
    {
        private readonly Func<int> _getCount;

        public string SourceBlock { get; }
        public string TargetBlock { get; }
        public int Capacity { get; }
        public int CurrentCount => _getCount();

        internal BufferMonitor(string sourceBlock, string targetBlock, int capacity, Func<int> getCount)
        {
            SourceBlock = sourceBlock;
            TargetBlock = targetBlock;
            Capacity = capacity;
            _getCount = getCount;
        }
    }
}
