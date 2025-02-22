namespace Uniun.DataFlow.Actor;

public interface IStreamProcessor<TInput>
{
    Task ProcessAsync(IAsyncEnumerable<TInput> input, CancellationToken cancellationToken);
}
