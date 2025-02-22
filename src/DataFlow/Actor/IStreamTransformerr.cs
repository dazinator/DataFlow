// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Actor;

public interface IStreamTransformer<TInput, TOutput>
{
    IAsyncEnumerable<TOutput> TransformAsync(IAsyncEnumerable<TInput> input, CancellationToken cancellationToken);
}
