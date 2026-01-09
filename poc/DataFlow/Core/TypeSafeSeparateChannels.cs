namespace DataFlow.POC.Core;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

/// <summary>
/// Type-safe separate channels - Option 2 from exploration.
/// Uses Channel&lt;TData&gt; and Channel&lt;TControl&gt; with compile-time type safety.
/// Eliminates runtime type checking overhead.
/// </summary>

/// <summary>
/// Dual-channel reader that provides separate data and control streams.
/// </summary>
public class DualChannelReader<TData, TControl>
{
    public ChannelReader<TData> DataReader { get; }
    public ChannelReader<TControl> ControlReader { get; }

    public DualChannelReader(
        ChannelReader<TData> dataReader,
        ChannelReader<TControl> controlReader)
    {
        DataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
        ControlReader = controlReader ?? throw new ArgumentNullException(nameof(controlReader));
    }

    /// <summary>
    /// Reads all items from both streams in a type-safe manner.
    /// Returns a tuple indicating which channel the item came from.
    /// </summary>
    public async IAsyncEnumerable<(TData? data, TControl? control, bool isData)> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var dataTask = ReadDataItemAsync(cancellationToken);
        var controlTask = ReadControlItemAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var completedTask = await Task.WhenAny(dataTask, controlTask).ConfigureAwait(false);

            if (completedTask == dataTask)
            {
                var (hasData, data) = await dataTask.ConfigureAwait(false);
                if (hasData)
                {
                    yield return (data, default, true);
                    dataTask = ReadDataItemAsync(cancellationToken);
                }
                else
                {
                    // Data channel completed, only read control
                    await foreach (var control in ControlReader.ReadAllAsync(cancellationToken))
                    {
                        yield return (default, control, false);
                    }
                    yield break;
                }
            }
            else // controlTask completed
            {
                var (hasControl, control) = await controlTask.ConfigureAwait(false);
                if (hasControl)
                {
                    yield return (default, control, false);
                    controlTask = ReadControlItemAsync(cancellationToken);
                }
                else
                {
                    // Control channel completed, only read data
                    await foreach (var data in DataReader.ReadAllAsync(cancellationToken))
                    {
                        yield return (data, default, true);
                    }
                    yield break;
                }
            }
        }
    }

    private async Task<(bool hasValue, TData? value)> ReadDataItemAsync(CancellationToken cancellationToken)
    {
        if (await DataReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (DataReader.TryRead(out var item))
            {
                return (true, item);
            }
        }
        return (false, default);
    }

    private async Task<(bool hasValue, TControl? value)> ReadControlItemAsync(CancellationToken cancellationToken)
    {
        if (await ControlReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (ControlReader.TryRead(out var item))
            {
                return (true, item);
            }
        }
        return (false, default);
    }
}

/// <summary>
/// Dual-channel writer that provides separate writers for data and control.
/// </summary>
public class DualChannelWriter<TData, TControl>
{
    public ChannelWriter<TData> DataWriter { get; }
    public ChannelWriter<TControl> ControlWriter { get; }

    public DualChannelWriter(
        ChannelWriter<TData> dataWriter,
        ChannelWriter<TControl> controlWriter)
    {
        DataWriter = dataWriter ?? throw new ArgumentNullException(nameof(dataWriter));
        ControlWriter = controlWriter ?? throw new ArgumentNullException(nameof(controlWriter));
    }

    /// <summary>
    /// Writes a data item to the data channel.
    /// </summary>
    public ValueTask WriteDataAsync(TData item, CancellationToken cancellationToken = default)
        => DataWriter.WriteAsync(item, cancellationToken);

    /// <summary>
    /// Writes a control signal to the control channel.
    /// </summary>
    public ValueTask WriteControlAsync(TControl item, CancellationToken cancellationToken = default)
        => ControlWriter.WriteAsync(item, cancellationToken);

    /// <summary>
    /// Completes both channels.
    /// </summary>
    public void Complete(Exception? error = null)
    {
        DataWriter.Complete(error);
        ControlWriter.Complete(error);
    }
}

/// <summary>
/// Edge strategy using type-safe separate channels for data and control.
/// No runtime type discrimination needed.
/// </summary>
public class TypeSafeSeparateChannelsStrategy<TData, TControl> : EdgeStrategy
{
    public TypeSafeSeparateChannelsStrategy(
        EdgeType edgeType = EdgeType.Broadcast,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        : base(edgeType, bufferMode, bufferCapacity)
    {
    }

    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        var writers = new Dictionary<IBlock, object>();
        var readers = new Dictionary<IBlock, object>();

        if (EdgeType == EdgeType.Broadcast)
        {
            // Create separate channels for each consumer
            foreach (var target in targetBlocks)
            {
                var dataChannel = CreateChannel<TData>();
                var controlChannel = CreateChannel<TControl>();

                var dualWriter = new DualChannelWriter<TData, TControl>(
                    dataChannel.Writer,
                    controlChannel.Writer);

                var dualReader = new DualChannelReader<TData, TControl>(
                    dataChannel.Reader,
                    controlChannel.Reader);

                writers[target] = dualWriter;
                readers[target] = dualReader;
            }
        }
        else if (EdgeType == EdgeType.Competing)
        {
            // Create shared data channel (competing) and individual control channels (broadcast)
            var sharedDataChannel = CreateChannel<TData>();
            
            foreach (var target in targetBlocks)
            {
                var controlChannel = CreateChannel<TControl>();

                var dualWriter = new DualChannelWriter<TData, TControl>(
                    sharedDataChannel.Writer,
                    controlChannel.Writer);

                var dualReader = new DualChannelReader<TData, TControl>(
                    sharedDataChannel.Reader,
                    controlChannel.Reader);

                writers[target] = dualWriter;
                readers[target] = dualReader;
            }
        }
        else
        {
            throw new NotSupportedException($"EdgeType {EdgeType} not supported in TypeSafeSeparateChannelsStrategy");
        }

        return (writers, readers);
    }

    public override Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // This method shouldn't be called for dual-channel strategy
        // Users should interact with DualChannelWriter directly
        throw new NotSupportedException(
            "RouteTypedItemAsync not supported for TypeSafeSeparateChannelsStrategy. " +
            "Use DualChannelWriter.WriteDataAsync or WriteControlAsync instead.");
    }

    private Channel<T> CreateChannel<T>()
    {
        return BufferMode switch
        {
            BufferMode.Bounded => Channel.CreateBounded<T>(new BoundedChannelOptions(BufferCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = EdgeType == EdgeType.Competing, // Optimize for competing edges
                SingleWriter = false
            }),
            BufferMode.None => Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleReader = EdgeType == EdgeType.Competing,
                SingleWriter = false
            }),
            _ => throw new ArgumentException($"Unsupported BufferMode: {BufferMode}")
        };
    }
}

/// <summary>
/// Block interface for dual-channel processing.
/// Blocks implementing this interface can handle separate data and control streams.
/// </summary>
public interface IDualChannelBlock<TDataIn, TDataOut, TControl>
{
    string Name { get; }

    /// <summary>
    /// Executes the block, processing data and control streams separately.
    /// </summary>
    Task<(IAsyncEnumerable<TDataOut> data, IAsyncEnumerable<TControl> control)> ExecuteAsync(
        IAsyncEnumerable<TDataIn> dataInput,
        IAsyncEnumerable<TControl> controlInput,
        CancellationToken cancellationToken);
}

/// <summary>
/// Helper factory for creating type-safe separate channel strategies.
/// </summary>
public static class TypeSafeSeparateChannelsFactory
{
    public static TypeSafeSeparateChannelsStrategy<TData, TControl> CreateBroadcast<TData, TControl>(
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        return new TypeSafeSeparateChannelsStrategy<TData, TControl>(
            EdgeType.Broadcast,
            bufferMode,
            bufferCapacity);
    }

    public static TypeSafeSeparateChannelsStrategy<TData, TControl> CreateCompeting<TData, TControl>(
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        return new TypeSafeSeparateChannelsStrategy<TData, TControl>(
            EdgeType.Competing,
            bufferMode,
            bufferCapacity);
    }
}
