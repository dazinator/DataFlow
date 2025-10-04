namespace Uniun.DataFlow.Blocks.Observable;

using Microsoft.Extensions.Logging;
using Uniun.DataFlow;

/// <summary>
/// A terminal block that outputs items to <see cref="IObserver{T}"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public class ObservableBlock<T> : BlockBase, ITargetBlock<T>
{
    private readonly IObserver<T> _observer;
    private ISourceBlock<T>? _source;

    public ObservableBlock(
        string name,
        ILogger<ObservableBlock<T>> logger,
       IObserver<T> observer,
        BlockOptions? options = null) : base(name, options, logger)
    {
        _observer = observer;

    }  

    public void SetSource(ISourceBlock<T> source)
    {
        _source = source;
    }

    private void EnsureSource()
    {
        if (_source is null)
        {
            throw new InvalidOperationException("No source block configured");
        }
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {      

        try
        {
            EnsureSource();
            await foreach (var item in _source!.GetAsyncEnumerable(this, context.CancellationToken))
            {
                _observer.OnNext(item);
                this.RecordOperation();
            }
            _observer.OnCompleted();
        }
        catch (Exception ex)
        {
            _observer.OnError(ex);
        }

    }
}
