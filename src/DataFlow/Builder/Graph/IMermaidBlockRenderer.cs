namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Interface for custom Mermaid rendering of specific block types.
/// Blocks can provide custom rendering logic by storing an instance of this interface in their metadata.
/// </summary>
public interface IMermaidBlockRenderer
{
    /// <summary>
    /// Determines if this renderer can handle the given block definition.
    /// </summary>
    /// <param name="block">The block definition to check</param>
    /// <returns>True if this renderer can handle the block, false otherwise</returns>
    bool CanRender(BlockDefinition block);

    /// <summary>
    /// Renders custom Mermaid content for the block using the provided context.
    /// The context provides callback methods that allow outer renderers to intercept and augment the content.
    /// </summary>
    /// <param name="context">The rendering context with callback methods</param>
    void RenderCustomContent(IBlockRenderContext context);
}
