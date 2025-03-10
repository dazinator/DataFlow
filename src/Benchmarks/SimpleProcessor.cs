namespace Benchmarks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


internal class SimpleProcessor : IStreamProcessor<int>
{
    private readonly Action<int> _onProcess;

    public SimpleProcessor(Action<int> onProcess)
    {
        _onProcess = onProcess;
    }

    public async Task ProcessAsync(IAsyncEnumerable<int> input, CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            _onProcess(item);
        }
    }
}

