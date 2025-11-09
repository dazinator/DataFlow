namespace Tests.DataFlow.Utils.Transformers;

using Uniun.DataFlow;

public class PassthroughTransformer<TInAndOut> : IStreamTransformer<TInAndOut, TInAndOut>
{
    public IAsyncEnumerable<TInAndOut> TransformAsync(
        IDataFlowContext context,
        IAsyncEnumerable<TInAndOut> input,
        CancellationToken cancellationToken)
    {
        return input;
    }
}
