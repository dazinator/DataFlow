namespace Tests.DataFlow.Utils.Transformers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Uniun.DataFlow;

public class NumberTransformer : IStreamTransformer<int, string>
{
    private readonly string _prefix;
    private readonly Action<string> _onTransform;

    public NumberTransformer(string prefix, Action<string> onTransform = null)
    {
        _prefix = prefix;
        _onTransform = onTransform;
    }

    public async IAsyncEnumerable<string> TransformAsync(IDataFlowContext context, IAsyncEnumerable<int> input, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var inputItem in input.WithCancellation(cancellationToken))
        {
            var result = $"{_prefix}-{inputItem}";
            _onTransform?.Invoke(result);
            yield return result;
        }

    }
}
