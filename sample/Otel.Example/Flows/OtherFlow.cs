using System.Runtime.CompilerServices;
using Uniun.DataFlow;
using Uniun.DataFlow.Actor;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Builder;

namespace Otel.Example.Flows;
internal class OtherFlowConfig : IDataFlowConfiguration
{
    public void Configure(DataFlowBuilder builder)
    {
        var options = new BlockOptions
        {
            MaxConcurrency = 4,
            Capacity = 10,

        };

        var items = Enumerable.Range(1, 10).Select(i => $"item{i}").ToArray();

        // Using the fluent API with type-safe linking
        builder.AddProducer("source", sp => new TestProducer<string>(items))
            .AddBatch<string>("batcher", maxBatchSize: 3, windowPeriod: TimeSpan.FromSeconds(10))
                 .ReceiveFrom("source")

            // .AddBatch<string>("batcher", maxBatchSize: 3, windowPeriod: TimeSpan.FromSeconds(10))           
            .AddProcessor<string[], TestProcessor<string[]>>("processor", sp => new TestProcessor<string[]>(
                onProcessItem: batch =>
                {
                    // do nothing

                }))
            .ReceiveFrom("batcher");


        //(builder.AddTransform<int, int, SlowTransformer>("transform"))
        //  .LinkTo();

        // Or if you prefer step by step:
        /*
        var source = builder.AddSource<int, LargeDataProducer>("source");
        var transform = builder.AddTransform<int, int, SlowTransformer>("transform");
        var collector = builder.AddProcessor<int, DataCollector>("collector");

        source.LinkTo(transform);
        transform.LinkTo(collector);
        */
    }

    public class TestProducer<T> : IStreamProducer<T>
    {
        private readonly IEnumerable<T> _items;
        private readonly Action<T>? _onItemProduced;
        private readonly TimeSpan _delay;

        public TestProducer(IEnumerable<T> items, Action<T>? onItemProduced = null, TimeSpan? delay = null)
        {
            _items = items;
            _onItemProduced = onItemProduced;
            _delay = delay ?? TimeSpan.FromMilliseconds(10);
        }

        public async IAsyncEnumerable<T> ProduceAsync(IDataFlowContext context,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            foreach (var item in _items)
            {
                cancellation.ThrowIfCancellationRequested();
                _onItemProduced?.Invoke(item);
                yield return item;
                await Task.Delay(_delay, cancellation);
            }
        }
    }



}



