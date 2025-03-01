// ReSharper disable once CheckNamespace

namespace Uniun.DataFlow.Blocks.Producer;

using System.Threading.Channels;
using Uniun.DataFlow;
using Uniun.DataFlow.Metrics;

/// <summary>
/// A block that produces items for downstream blocks to consume.
/// </summary>
/// <typeparam name="TOutput"></typeparam>
public class ProducerBlock<TOutput> : BlockBase, ISourceBlock<TOutput>
{
    private readonly Func<IDataFlowContext, CancellationToken, Task<IEnumerable<IStreamProducer<TOutput>>>> _producersFactory;
    private readonly BoundedChannelOptions _outputChannelOptions;
    private readonly Channel<TOutput> _outputChannel;

    public ProducerBlock(
        string name,
        Func<IDataFlowContext, CancellationToken, Task<IEnumerable<IStreamProducer<TOutput>>>> producersFactory,
        BlockOptions? options = null) : base(name, options)
    {
        _producersFactory = producersFactory;
        _outputChannelOptions = Options.ChannelOptions ?? new BoundedChannelOptions(100);
        _outputChannel = Channel.CreateBounded<TOutput>(_outputChannelOptions);     
    }

   // private ChannelReader<TOutput> Reader => _outputChannel.Reader;

    public ChannelReader<TOutput> GetReader(ITargetBlock<TOutput> target)
    {
        return _outputChannel.Reader;
    }  

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        try
        {

            // thought: if we wanted truly dynamic producers (instead of providing them upfront before the data is processed),
            // we could use a router block.
            var producers = await _producersFactory(context, context.CancellationToken);
            var producersArray = producers.ToArray();

            using var monitoredChannel = this.CreateMonitoredChannel(_outputChannelOptions.Capacity, context, _outputChannel);
            await ExecuteParallelActivities(context, producers.Count(), async (index, ctx) =>
            {
                // its safe to concurrently index into a list that isn't being modified.  
                var producer = producersArray[index];
                await foreach (var item in producer.ProduceAsync(ctx.CancellationToken))
                {
                    await _outputChannel.Writer.WriteAsync(item, ctx.CancellationToken);
                }
            });
        }
        finally
        {
            _outputChannel.Writer.Complete();
        }
    }
}
