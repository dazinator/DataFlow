namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Builder for propagator blocks (blocks that both receive and produce) in the structured dataflow.
/// Supports both ReceiveFrom and LinkTo for fluent API.
/// </summary>
public class StructuredPropagatorBlockBuilder<TIn, TOut>
{
    private readonly string _blockName;

    public StructuredPropagatorBlockBuilder(IStructuredDataFlowBuilder builder, string blockName)
    {
        Builder = builder;
        _blockName = blockName;
    }

    /// <summary>
    /// Makes this block receive data from the specified source block.
    /// </summary>
    public StructuredPropagatorBlockBuilder<TIn, TOut> ReceiveFrom(string sourceBlockName)
    {
        Builder.AddConnection(sourceBlockName, _blockName, typeof(TIn));
        return this;
    }

    /// <summary>
    /// Makes this block receive data from the last added source block.
    /// </summary>
    public StructuredPropagatorBlockBuilder<TIn, TOut> ReceiveFromLast()
    {
        var lastSourceBlock = Builder.GetLastSourceBlockName() ?? throw new InvalidOperationException("No previous source block to receive from");

        return ReceiveFrom(lastSourceBlock);
    }

    /// <summary>
    /// Links this block to a target block by name.
    /// </summary>
    public StructuredPropagatorBlockBuilder<TIn, TOut> LinkTo(string targetBlockName)
    {
        Builder.AddConnection(_blockName, targetBlockName, typeof(TOut));
        return this;
    }

    /// <summary>
    /// Adds a processor block and links it to this propagator.
    /// </summary>
    public StructuredTargetBlockBuilder<TOut> AddProcessor(
        string name,
        Func<IServiceProvider, IStreamProcessor<TOut>> processorFactory,
        BlockOptions? options = null)
    {
        var targetBuilder = Builder.AddProcessor(name, processorFactory, options);
        Builder.AddConnection(_blockName, name, typeof(TOut));
        return targetBuilder;
    }

    /// <summary>
    /// Adds a transform block and links it to this propagator.
    /// </summary>
    public StructuredPropagatorBlockBuilder<TOut, TNewOut> AddTransform<TNewOut>(
        string name,
        Func<IServiceProvider, IStreamTransformer<TOut, TNewOut>> transformerFactory,
        BlockOptions? options = null)
    {
        var propagatorBuilder = Builder.AddTransform(name, transformerFactory, options);
        Builder.AddConnection(_blockName, name, typeof(TOut));
        return propagatorBuilder;
    }

    /// <summary>
    /// Adds a batch block and links it to this propagator.
    /// </summary>
    public StructuredPropagatorBlockBuilder<TOut, TOut[]> AddBatch(
        string name,
        int maxBatchSize,
        TimeSpan? windowPeriod = null,
        BlockOptions? options = null)
    {
        var propagatorBuilder = Builder.AddBatch<TOut>(name, maxBatchSize, windowPeriod, options);
        Builder.AddConnection(_blockName, name, typeof(TOut));
        return propagatorBuilder;
    }

    /// <summary>
    /// Marks this propagator block as an entry block.
    /// Entry blocks are used by routing blocks to identify where to send routed items.
    /// Since propagator blocks are also target blocks, they can serve as entry points.
    /// </summary>
    public StructuredPropagatorBlockBuilder<TIn, TOut> AsEntry()
    {
        // Get the block definition and mark it as an entry block
        var blockDef = Builder.Graph.GetBlockDefinition(_blockName);
        blockDef.IsEntryBlock = true;

        // If this is a route builder, also set it as the entry block
        if (Builder is IRouteBuilder routeBuilder)
        {
            routeBuilder.SetEntryBlock(_blockName);
        }

        return this;
    }

    /// <summary>
    /// Gets the underlying builder for additional operations.
    /// </summary>
    public IStructuredDataFlowBuilder Builder { get; }
}
