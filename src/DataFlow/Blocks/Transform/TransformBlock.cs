// ReSharper disable once CheckNamespace

namespace Uniun.DataFlow.Blocks.Transform;

using System.Resources;
using System.Threading.Channels;
using Uniun.DataFlow;
using Uniun.DataFlow.Actor;

// Transform block for data transformation
public class TransformBlock<TIn, TOut> : BlockBase, IPropagatorBlock<TIn, TOut>
{
    private readonly Func<IServiceProvider, IStreamTransformer<TIn, TOut>> _transformerFactory;
    private readonly Channel<TOut> _output;
    private ISourceBlock<TIn>? _source;

    public TransformBlock(
        string name,
        Func<IServiceProvider, IStreamTransformer<TIn, TOut>> transformerFactory,
        BlockOptions? options = null
    ) : base(name, options)
    {
        _transformerFactory = transformerFactory;
        _output = Channel.CreateBounded<TOut>(Options.ChannelOptions ?? new BoundedChannelOptions(100));
    }   

    private ChannelReader<TOut> Reader => _output.Reader;

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
        return Reader;
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        EnsureSourceReader();

        try
        {           
            await ExecuteParallelActivities(context, Options.MaxConcurrency, ExecuteStreamTransformerAsync);
           
            // Source channel should be fully drained
            await SourceReader.Completion;
        }
        finally
        {
            // ensure channel is completed so downstream blocks will know there is no more data expected.
            _output.Writer.Complete();
        }
    }


    protected async Task ExecuteStreamTransformerAsync(int index, IDataFlowContext context)
    {
        var transformer = _transformerFactory(context.ServiceProvider);
        var input = SourceReader.ReadAllAsync(context.CancellationToken);

        await foreach (var result in transformer.TransformAsync(input, context.CancellationToken)
            .WithCancellation(context.CancellationToken))
        {
            await _output.Writer.WriteAsync(result, context.CancellationToken);
        }
    }

  
}
