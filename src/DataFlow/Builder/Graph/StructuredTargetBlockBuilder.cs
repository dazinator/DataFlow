namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Builder for target blocks in the structured dataflow.
/// Supports ReceiveFrom for fluent API.
/// </summary>
public class StructuredTargetBlockBuilder<TIn>
{
    private readonly string _blockName;

    public StructuredTargetBlockBuilder(IStructuredDataFlowBuilder builder, string blockName)
    {
        Builder = builder;
        _blockName = blockName;
    }

    /// <summary>
    /// Makes this target block receive data from the specified source block.
    /// </summary>
    public StructuredTargetBlockBuilder<TIn> ReceiveFrom(string sourceBlockName)
    {
        Builder.AddConnection(sourceBlockName, _blockName, typeof(TIn));
        return this;
    }

    /// <summary>
    /// Makes this target block receive data from the last added source block.
    /// </summary>
    public StructuredTargetBlockBuilder<TIn> ReceiveFromLast()
    {
        var lastSourceBlock = Builder.GetLastSourceBlockName() ?? throw new InvalidOperationException("No previous source block to receive from");

        return ReceiveFrom(lastSourceBlock);
    }

    /// <summary>
    /// Marks this target block as an entry block.
    /// Entry blocks are used by routing blocks to identify where to send routed items.
    /// </summary>
    public StructuredTargetBlockBuilder<TIn> AsEntry()
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
