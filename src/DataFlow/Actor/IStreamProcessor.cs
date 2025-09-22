namespace Uniun.DataFlow.Actor;

public interface IStreamProcessor<TInput>
{
    Task ProcessAsync(IDataFlowContext context, IAsyncEnumerable<TInput> input, CancellationToken cancellationToken);
}
