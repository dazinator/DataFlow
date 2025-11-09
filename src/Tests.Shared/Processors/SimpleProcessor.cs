namespace Benchmarks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Uniun.DataFlow;


public class SimpleProcessor : IStreamProcessor<int>
{
    private readonly Action<int> _onProcess;

    public SimpleProcessor(Action<int> onProcess)
    {
        _onProcess = onProcess;
    }

    public async Task ProcessAsync(IDataFlowContext context, IAsyncEnumerable<int> input, CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            _onProcess(item);
        }
    }
}

public class SimpleProcessor<T> : IStreamProcessor<T>
{
    private readonly Action<T> _onProcess;

    public SimpleProcessor(Action<T> onProcess)
    {
        _onProcess = onProcess;
    }

    public async Task ProcessAsync(IDataFlowContext context, IAsyncEnumerable<T> input, CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            _onProcess(item);
        }
    }
}

