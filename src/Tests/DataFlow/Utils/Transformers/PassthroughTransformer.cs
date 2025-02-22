namespace Tests.DataFlow.Utils.Transformers;
public class PassthroughTransformer<TInAndOut> : IStreamTransformer<TInAndOut, TInAndOut>
{
    public IAsyncEnumerable<TInAndOut> TransformAsync(
        IAsyncEnumerable<TInAndOut> input,
        CancellationToken cancellationToken)
    {
        return input;
    }
}
