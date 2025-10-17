using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Uniun.DataFlow;
using Uniun.DataFlow.Actor;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Builder;

namespace Otel.Example.Flows;
internal class ExampleFlowConfig : IDataFlowConfiguration
{
    public void Configure(DataFlowBuilder builder)
    {
        var options = new BlockOptions
        {
            MaxConcurrency = 4,
            Capacity = 10,

        };

        // Using the fluent API with type-safe linking
        builder
            .AddProducer<int, LargeDataProducer>("source")
            .AddTransform<int, int, SlowTransformer>("transform")
                .ReceiveFrom("source")
                    .AddProcessor<int, TestProcessor<int>>("processor", sp => new TestProcessor<int>(
                onProcessItem: batch =>
                {
                    // do nothing

                }))
            .ReceiveFrom("transform");

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

    internal class LargeDataProducer : IStreamProducer<int>
    {
        public async IAsyncEnumerable<int> ProduceAsync(IDataFlowContext context,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            foreach (var i in Enumerable.Range(0, 1000))
            {
                cancellation.ThrowIfCancellationRequested();
                yield return i;
            }
        }

    }

    internal class SlowTransformer : IStreamTransformer<int, int>
    {
        public async IAsyncEnumerable<int> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<int> input,
            CancellationToken cancellationToken)
        {
            await foreach (var item in input)
            {
                await Task.Delay(10, cancellationToken); // Simulate some work
                yield return item;
            }
        }
    }

}


