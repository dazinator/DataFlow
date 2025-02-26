// ReSharper disable once CheckNamespace

namespace Uniun.DataFlow.Blocks.Producer;

using System.Threading.Channels;
using Uniun.DataFlow;

/// <summary>
/// A block that produces items for downstream blocks to consume.
/// </summary>
/// <typeparam name="TOutput"></typeparam>
public class ProducerBlock<TOutput> : BlockBase, ISourceBlock<TOutput>
{
    private readonly Func<IDataFlowContext, CancellationToken, Task<IEnumerable<IStreamProducer<TOutput>>>> _producersFactory;
    private readonly Channel<TOutput> _channel;

    public ProducerBlock(
        string name,
        Func<IDataFlowContext, CancellationToken, Task<IEnumerable<IStreamProducer<TOutput>>>> producersFactory,
        BlockOptions? options = null) : base(name, options)
    {
        _producersFactory = producersFactory;
        _channel = Channel.CreateBounded<TOutput>(GetChannelOptions(Options));
    }

    public ChannelReader<TOutput> Reader => _channel.Reader;

    private BoundedChannelOptions GetChannelOptions(BlockOptions? options)
    {
        var channelOptions = options?.ChannelOptions ?? new BoundedChannelOptions(100);
        return channelOptions;
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        try
        {

            // thought: if we wanted truly dynamic producers (instead of providing them upfront before the data is processed),
            // we could use a router block.
            var producers = await _producersFactory(context, context.CancellationToken);
            var producersArray = producers.ToArray();

            await ExecuteParallelActivities(context, producers.Count(), async (index, ctx) =>
            {
                // its safe to concurrently index into a list that isn't being modified.  
                var producer = producersArray[index];
                await foreach (var item in producer.ProduceAsync(ctx.CancellationToken))
                {
                    await _channel.Writer.WriteAsync(item, ctx.CancellationToken);
                }
            });
        }
        finally
        {
            _channel.Writer.Complete();
        }
    }
}
