// ReSharper disable once CheckNamespace

namespace Uniun.DataFlow.Blocks.Processor;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;
using Uniun.DataFlow.Actor;

/// <summary>
/// Target block for terminal operations on data items.
/// Actor based block that will instantiate concurrent <see cref="IStreamProcessor{TInput}"/> actors (upto max concurrency) to process items from the source block.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ProcessorBlock<T> : BlockBase, ITargetBlock<T>
{
    private readonly Func<IServiceProvider, IStreamProcessor<T>> _processorFactory;
    private ISourceBlock<T> _source;

    public ProcessorBlock(
        string name,
         ILogger<ProcessorBlock<T>> logger,
        Func<IServiceProvider, IStreamProcessor<T>> processorFactory,
        BlockOptions? options = null
    ) : base(name, options, logger)
    {
        _processorFactory = processorFactory;
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
        // await base.CoreExecuteAsync(context);
        EnsureSourceReader();
        await ExecuteParallelActivities(context, Options.MaxConcurrency, ExecuteStreamProcessorAsync);
    }

    // 1. Add detailed logging in ProcessorBlock.ExecuteStreamProcessorAsync
    // This shows when actors are starting and ending in the processor block
    protected async Task ExecuteStreamProcessorAsync(int index, IDataFlowContext context)
    {
        // Will use scoped ServiceProvider if UseSeperateScopes=true
        var processor = _processorFactory(context.ServiceProvider);
        var input = SourceReader.ReadAllAsync(context.CancellationToken);
        if (Options.EnableFlowRateMetrics)
        {
            input = input.DecorateWithCallbackAfterEachItem(
                this.RecordOperation,
                context.CancellationToken
            );
        }


        await processor.ProcessAsync(context, input, context.CancellationToken);
    }
}
