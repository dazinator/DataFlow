namespace DataFlow.POC.Registry;

/// <summary>
/// Metadata about a registered block type.
/// Provides type information for introspection and validation.
/// </summary>
public interface IBlockTypeMetadata
{
    /// <summary>
    /// The input type of the block.
    /// </summary>
    Type InputType { get; }

    /// <summary>
    /// The output type of the block.
    /// </summary>
    Type OutputType { get; }
}
