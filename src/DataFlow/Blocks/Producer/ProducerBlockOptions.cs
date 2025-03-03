// ReSharper disable once CheckNamespace

namespace Uniun.DataFlow.Blocks.Producer;

using Uniun.DataFlow;

public class ProducerBlockOptions<TOutput>: BlockOptions
{
    public Func<IDataFlowContext, CancellationToken, Task<IEnumerable<IStreamProducer<TOutput>>>> ProducersFactory { get; set; }
}
