namespace DataFlow.POC.Core;

/// <summary>
/// Standard implementation of IBlockContext.
/// </summary>
public sealed class BlockContext : IBlockContext
{
    /// <inheritdoc />
    public string BlockName { get; }
    
    /// <inheritdoc />
    public IReadOnlyDictionary<string, object>? Metadata { get; }

    public BlockContext(string blockName, IReadOnlyDictionary<string, object>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(blockName);
        BlockName = blockName;
        Metadata = metadata;
    }

    /// <summary>
    /// Creates a block context with the specified name.
    /// </summary>
    public static BlockContext Create(string blockName)
        => new(blockName);

    /// <summary>
    /// Creates a block context with name and metadata.
    /// </summary>
    public static BlockContext Create(string blockName, IReadOnlyDictionary<string, object> metadata)
        => new(blockName, metadata);
}
