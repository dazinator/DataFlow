// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks.Transform;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

/// <summary>
/// Transform block that performs transformation inline with downstream consumption.
/// 
/// KEY CONCEPT: This block eliminates doing work in ExecuteAsync. Instead, all transformation
/// work happens during GetAsyncEnumerable enumeration by the downstream block.
/// 
/// How it works:
/// 1. ExecuteAsync: Completes immediately - does no work
/// 2. GetAsyncEnumerable: Pulls from upstream, transforms, and yields inline
/// 3. The transformation happens in the downstream block's execution context
/// 
/// This provides true inline execution with zero buffering overhead.
/// The dataflow completes when the downstream target block finishes executing.
/// 
/// Note: This block is completely channel-free. All target blocks (ProcessorBlock, OutputBlock,  
/// ObservableBlock) use GetAsyncEnumerable() directly for channel-free operation.
/// </summary>
/// <typeparam name="TIn">Input type</typeparam>
/// <typeparam name="TOut">Output type</typeparam>
public class InlineTransformBlock<TIn, TOut> : BlockBase, IPropagatorBlock<TIn, TOut>
{
    private readonly Func<TIn, TOut> _transformFunc;
    private ISourceBlock<TIn>? _source;
    private IDataFlowContext? _executionContext;

    public InlineTransformBlock(
        string name,
        ILogger<InlineTransformBlock<TIn, TOut>> logger,
        Func<TIn, TOut> transformFunc,
        BlockOptions? options = null
    ) : base(name, options, logger)
    {
        _transformFunc = transformFunc;
    }

    public void SetSource(ISourceBlock<TIn> source)
    {
        _source = source;
        (source as IExpectsDownstreamTargets)?.RegisterExpectedTarget();
    }

    private void EnsureSource()
    {
        if (_source is null)
        {
            throw new InvalidOperationException("No source block configured");
        }
    }

    public async IAsyncEnumerable<TOut> GetAsyncEnumerable(
        ITargetBlock<TOut> target,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureSource();

        // THIS IS WHERE ALL THE WORK HAPPENS - inline with downstream consumption
        // Pull from upstream and transform on-the-fly without any buffering
        await foreach (var item in _source!.GetAsyncEnumerable(this, cancellationToken))
        {
            var transformed = _transformFunc(item);
            RecordOperation();
            yield return transformed;
        }
    }

    protected override Task CoreExecuteAsync(IDataFlowContext context)
    {
        EnsureSource();
        _executionContext = context;

        // ExecuteAsync completes immediately for inline execution.
        // All transformation work happens inline during GetAsyncEnumerable() enumeration
        // by the downstream block. The dataflow completes when the downstream target block
        // finishes executing.
        return Task.CompletedTask;
    }
}
