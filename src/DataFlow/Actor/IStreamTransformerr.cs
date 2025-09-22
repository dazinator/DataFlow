// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Actor;

public interface IStreamTransformer<TInput, TOutput>
{
    IAsyncEnumerable<TOutput> TransformAsync(IDataFlowContext context, IAsyncEnumerable<TInput> input, CancellationToken cancellationToken);
}
