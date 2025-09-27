namespace Tests.DataFlow.Utils.Transformers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class TestTransformer<TIn, TOut> : IStreamTransformer<TIn, TOut>
{
    private readonly Func<TIn, TOut> _onTransform;

    public TestTransformer(Func<TIn, TOut> onTransform = null)
    {
        _onTransform = onTransform;
    }

    public async IAsyncEnumerable<TOut> TransformAsync(IDataFlowContext context, IAsyncEnumerable<TIn> input, CancellationToken cancellationToken)
    {
        await foreach (var inputItem in input.WithCancellation(cancellationToken))
        {
            var result = _onTransform.Invoke(inputItem);
            yield return result;
        }

    }
}
