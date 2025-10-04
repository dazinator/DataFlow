// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks.Transform;

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

/// <summary>
/// Example of a minimal-buffer relay transform block.
/// This demonstrates a pattern for relaying items with minimal buffering (capacity of 1)
/// to maintain tight backpressure while avoiding large output buffers.
/// 
/// Key concepts:
/// 1. Uses a Channel with capacity of 1 for minimal buffering
/// 2. Provides both ChannelReader and IAsyncEnumerable interfaces
/// 3. Demonstrates tight backpressure - if downstream is slow, upstream naturally slows
/// 
/// This pattern is useful when:
/// - You want to minimize memory usage from buffering
/// - You want tight backpressure propagation through the pipeline
/// - The transformation is lightweight and doesn't need concurrent actors
/// </summary>
/// <typeparam name="TIn">Input type</typeparam>
/// <typeparam name="TOut">Output type</typeparam>
public class MinimalBufferTransformBlock<TIn, TOut> : BlockBase, IPropagatorBlock<TIn, TOut>
{
    private readonly Func<TIn, TOut> _transformFunc;
    private readonly Channel<TOut> _outputChannel;
    private ISourceBlock<TIn>? _source;

    public MinimalBufferTransformBlock(
        string name,
        ILogger<MinimalBufferTransformBlock<TIn, TOut>> logger,
        Func<TIn, TOut> transformFunc,
        BlockOptions? options = null
    ) : base(name, options, logger)
    {
        _transformFunc = transformFunc;
        
        // Key: Use capacity of 1 for minimal buffering
        _outputChannel = Channel.CreateBounded<TOut>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait, // This ensures backpressure
            SingleReader = true,  // Optimization: only one downstream reader
            SingleWriter = true   // Optimization: only one upstream writer
        });
    }

    public void SetSource(ISourceBlock<TIn> source)
    {
        _source = source;
    }

    private void EnsureSource()
    {
        if (_source is null)
        {
            throw new InvalidOperationException("No source block configured");
        }
    }

    public ChannelReader<TOut> GetReader(ITargetBlock<TOut> target)
    {
        return _outputChannel.Reader;
    }

    public async IAsyncEnumerable<TOut> GetAsyncEnumerable(
        ITargetBlock<TOut> target,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Simply read from the minimal channel
        await foreach (var item in _outputChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        EnsureSource();

        try
        {
            // Read from upstream and transform, writing to minimal buffer
            await foreach (var item in _source.GetAsyncEnumerable(this, context.CancellationToken))
            {
                var transformed = _transformFunc(item);
                
                // This write will block if downstream hasn't read the previous item yet
                // This is the backpressure in action
                await _outputChannel.Writer.WriteAsync(transformed, context.CancellationToken);
                RecordOperation();
            }
        }
        finally
        {
            _outputChannel.Writer.Complete();
        }
    }
}

