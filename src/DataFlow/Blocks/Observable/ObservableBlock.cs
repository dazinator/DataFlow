namespace Uniun.DataFlow.Blocks.Observable;

using System.Threading.Channels;
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

    public void SetSource(ISourceBlock<T> source)
    {
        _source = source;
        SourceReader = _source.GetReader(this);
    }
    public ChannelReader<T> SourceReader { get; private set; }
    private void EnsureSourceReader()
    {
        if (_source is null)
        {
            throw new InvalidOperationException("No source block configured");
        }
        if (SourceReader is null)
        {
            throw new InvalidOperationException("No source reader configured");
        }
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {      

        try
        {
            EnsureSourceReader();
            await foreach (var item in SourceReader.ReadAllAsync(context.CancellationToken))
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
