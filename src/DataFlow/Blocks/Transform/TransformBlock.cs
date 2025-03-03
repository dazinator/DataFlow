// ReSharper disable once CheckNamespace

namespace Uniun.DataFlow.Blocks.Transform;

using System.Threading.Channels;
using Uniun.DataFlow;
using Uniun.DataFlow.Actor;
using Uniun.DataFlow.Metrics;

// Transform block for data transformation

public class TransformBlockOptions<TIn, TOut> : BlockOptions
{
    public Func<IServiceProvider, IStreamTransformer<TIn, TOut>> TransformerFactory { get; set; }
}
public class TransformBlock<TIn, TOut> : BlockBase, IPropagatorBlock<TIn, TOut>
{
    private readonly Func<IServiceProvider, IStreamTransformer<TIn, TOut>> _transformerFactory;
    private readonly IBoundedChannelFactory _channelFactory;   
    private readonly MonitoredChannel<TOut> _outputChannel;
    private ISourceBlock<TIn>? _source;

    public TransformBlock(
        string name,
        IBoundedChannelFactory channelFactory,
        TransformBlockOptions<TIn, TOut> options
    ) : base(name, options)
    {
        _transformerFactory = options.TransformerFactory;
        _channelFactory = channelFactory;
        _outputChannel = _channelFactory.CreateMonitoredChannel<TOut>(name, options?.Capacity);
    }   

   // private ChannelReader<TOut> Reader => _output.Reader;

    public void SetSource(ISourceBlock<TIn> source)
    {
        _source = source;
        SourceReader = _source.GetReader(this);
    }
    public ChannelReader<TIn> SourceReader { get; private set; }
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

    public ChannelReader<TOut> GetReader(ITargetBlock<TOut> target)
    {
        return _outputChannel.Reader;
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        EnsureSourceReader();

        try
        {
            _outputChannel.StartMonitoring(context);
            // var monitoredChannel = this.CreateMonitoredChannel(_outputChannelOptions.Capacity, context, _outputChannel);
            await ExecuteParallelActivities(context, Options.MaxConcurrency, ExecuteStreamTransformerAsync);
           
            // Source channel should be fully drained
            await SourceReader.Completion;
        }
        finally
        {
            // ensure channel is completed so downstream blocks will know there is no more data expected.
            _outputChannel.Writer.Complete();
        }
    }


    protected async Task ExecuteStreamTransformerAsync(int index, IDataFlowContext context)
    {
        var transformer = _transformerFactory(context.ServiceProvider);
        var input = SourceReader.ReadAllAsync(context.CancellationToken);

        await foreach (var result in transformer.TransformAsync(input, context.CancellationToken)
            .WithCancellation(context.CancellationToken))
        {
            await _outputChannel.Writer.WriteAsync(result, context.CancellationToken);
        }
    }

  
}
