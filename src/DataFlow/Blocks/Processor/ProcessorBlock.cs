// ReSharper disable once CheckNamespace

namespace Uniun.DataFlow.Blocks.Processor;

using Uniun.DataFlow;
using Uniun.DataFlow.Actor;

/// <summary>
/// Target block for terminal operations on data items.
/// Actor based block that will instantiate concurrent <see cref="IStreamProcessor{TInput}"/> actors (upto max concurrency) to process items from the source block.
/// </summary>
/// <typeparam name="TInput"></typeparam>
public class ProcessorBlock<TInput> : BlockBase, ITargetBlock<TInput>
{
    private readonly Func<IServiceProvider, IStreamProcessor<TInput>> _processorFactory;
    private ISourceBlock<TInput> _source;

    public ProcessorBlock(
        string name,
        Func<IServiceProvider, IStreamProcessor<TInput>> processorFactory,
        BlockOptions? options = null
    ) : base(name, options)
    {
        _processorFactory = processorFactory;
    }


    public void SetSource(ISourceBlock<TInput> source)
    {
        _source = source;
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        // await base.CoreExecuteAsync(context);
        await ExecuteParallelActivities(context, Options.MaxConcurrency, ExecuteStreamProcessorAsync);
        await _source.Reader.Completion;
    }

    protected async Task ExecuteStreamProcessorAsync(int index, IDataFlowContext context)
    {
        if (_source == null)
        {
            throw new InvalidOperationException("No source block configured");
        }

        // Will use scoped ServiceProvider if UseSeperateScopes=true
        var processor = _processorFactory(context.ServiceProvider);
        var input = _source!.Reader.ReadAllAsync(context.CancellationToken);
        await processor.ProcessAsync(input, context.CancellationToken);
    }
}
