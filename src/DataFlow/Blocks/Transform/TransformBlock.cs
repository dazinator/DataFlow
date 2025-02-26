// ReSharper disable once CheckNamespace

namespace Uniun.DataFlow.Blocks.Transform;

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

    public void SetSource(ISourceBlock<TIn> source)
    {
        _source = source;
    }

    public ChannelReader<TOut> Reader => _output.Reader;

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        if (_source == null)
        {
            throw new InvalidOperationException("No source block configured");
        }

        try
        {

            await ExecuteParallelActivities(context, Options.MaxConcurrency, ExecuteStreamTransformerAsync);

            // Source channel should be fully drained
            await _source!.Reader.Completion;
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
        var input = _source!.Reader.ReadAllAsync(context.CancellationToken);

        await foreach (var result in transformer.TransformAsync(input, context.CancellationToken)
            .WithCancellation(context.CancellationToken))
        {
            await _output.Writer.WriteAsync(result, context.CancellationToken);
        }
    }

}
