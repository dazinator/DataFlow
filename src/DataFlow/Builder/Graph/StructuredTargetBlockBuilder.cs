namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Builder for target blocks in the structured dataflow.
/// Supports ReceiveFrom for fluent API.
/// </summary>
public class StructuredTargetBlockBuilder<TIn>
{
    private readonly IStructuredDataFlowBuilder _builder;
    private readonly string _blockName;

    public StructuredTargetBlockBuilder(IStructuredDataFlowBuilder builder, string blockName)
    {
        _builder = builder;
        _blockName = blockName;
    }

    /// <summary>
    /// Makes this target block receive data from the specified source block.
    /// </summary>
    public StructuredTargetBlockBuilder<TIn> ReceiveFrom(string sourceBlockName)
    {
        _builder.AddConnection(sourceBlockName, _blockName, typeof(TIn));
        return this;
    }

    /// <summary>
    /// Makes this target block receive data from the last added source block.
    /// </summary>
    public StructuredTargetBlockBuilder<TIn> ReceiveFromLast()
    {
        var lastSourceBlock = _builder.GetLastSourceBlockName();
        if (lastSourceBlock == null)
        {
            throw new InvalidOperationException("No previous source block to receive from");
        }

        return ReceiveFrom(lastSourceBlock);
    }

    /// <summary>
    /// Gets the underlying builder for additional operations.
    /// </summary>
    public IStructuredDataFlowBuilder Builder => _builder;
}
