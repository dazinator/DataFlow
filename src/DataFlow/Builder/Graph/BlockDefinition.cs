namespace Uniun.DataFlow.Builder.Graph;

using Uniun.DataFlow.Blocks;

/// <summary>
/// Represents a block definition in the dataflow graph before the block is instantiated.
/// This metadata describes what block to create and how to create it.
/// </summary>
public class BlockDefinition
{
    private bool? _isSourceBlock;
    private bool? _isTargetBlock;

    public BlockDefinition(string name, Type blockType, Func<IServiceProvider, IBlock> factory)
    {
        Name = name;
        BlockType = blockType;
        Factory = factory;
    }

    /// <summary>
    /// The unique name of the block in the dataflow.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The type of block (e.g., ISourceBlock<T>, ITargetBlock<T>, etc.)
    /// </summary>
    public Type BlockType { get; }

    /// <summary>
    /// Factory function to create the block instance during Build().
    /// </summary>
    public Func<IServiceProvider, IBlock> Factory { get; }

    /// <summary>
    /// The type of data this block outputs (null for terminal blocks).
    /// </summary>
    public Type? OutputType { get; init; }

    /// <summary>
    /// The type of data this block receives (null for source blocks).
    /// </summary>
    public Type? InputType { get; init; }

    /// <summary>
    /// Metadata about the block that can be used by interceptors or for documentation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Determines if this block implements ISourceBlock interface.
    /// Result is cached after first call.
    /// </summary>
    public bool IsSourceBlock()
    {
        if (!_isSourceBlock.HasValue)
        {
            _isSourceBlock = BlockType.GetInterfaces().Any(i => 
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISourceBlock<>));
        }
        return _isSourceBlock.Value;
    }

    /// <summary>
    /// Determines if this block implements ITargetBlock interface.
    /// Result is cached after first call.
    /// </summary>
    public bool IsTargetBlock()
    {
        if (!_isTargetBlock.HasValue)
        {
            _isTargetBlock = BlockType.GetInterfaces().Any(i => 
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITargetBlock<>));
        }
        return _isTargetBlock.Value;
    }
}
