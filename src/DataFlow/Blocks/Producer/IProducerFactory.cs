// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks.Producer;

public interface IProducerFactory<TOutput>
{
    IAsyncEnumerable<IStreamProducer<TOutput>> CreateProducersAsync(IDataFlowContext context, CancellationToken cancellation);
}
