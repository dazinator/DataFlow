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
    private readonly MonitoredChannel<TOutput> _outputChannel;

    public ProducerBlock(
        string name,
         IBoundedChannelFactory channelFactory,
        ProducerBlockOptions<TOutput> options) : base(name, options)
    {
        _producersFactory = options.ProducersFactory;
        _outputChannel = channelFactory.CreateMonitoredChannel<TOutput>(name, options.Capacity);   
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
            _outputChannel.StartMonitoring(context);
            var producers = await _producersFactory(context, context.CancellationToken);
            var producersArray = producers.ToArray();

          //  using var monitoredChannel = this.CreateMonitoredChannel(_outputChannel.Capacity, context, _outputChannel);
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
