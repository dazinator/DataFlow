namespace Uniun.DataFlow.Blocks.Observable;

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
       IObserver<T> observer,
        BlockOptions? options = null) : base(name, options)
    {
        _observer = observer;
    }

    public void SetSource(ISourceBlock<T> source) => _source = source;

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        if (_source == null)
        {
            throw new InvalidOperationException("No source block configured");
        }

        try
        {
            await foreach (var item in _source.Reader.ReadAllAsync(context.CancellationToken))
            {
                _observer.OnNext(item);
            }
            _observer.OnCompleted();
        }
        catch (Exception ex)
        {
            _observer.OnError(ex);
        }

    }
}
