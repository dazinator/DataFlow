namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Builder for source blocks in the structured dataflow.
/// Supports LinkTo for fluent API.
/// </summary>
public class StructuredSourceBlockBuilder<TOut>
{
    private readonly string _blockName;

    public StructuredSourceBlockBuilder(IStructuredDataFlowBuilder builder, string blockName)
    {
        Builder = builder;
        _blockName = blockName;
    }

    /// <summary>
    /// Links this source block to a target block by name.
    /// </summary>
    public StructuredSourceBlockBuilder<TOut> LinkTo(string targetBlockName)
    {
        Builder.AddConnection(_blockName, targetBlockName, typeof(TOut));
        return this;
    }

    /// <summary>
    /// Adds a processor block and links it to this source.
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
    /// Adds a transform block and links it to this source.
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
    /// Adds a batch block and links it to this source.
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
    /// Gets the underlying builder for additional operations.
    /// </summary>
    public IStructuredDataFlowBuilder Builder { get; }
}
