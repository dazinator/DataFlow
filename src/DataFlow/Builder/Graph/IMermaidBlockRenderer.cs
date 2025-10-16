namespace Uniun.DataFlow.Builder.Graph;

using System.Text;

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
    /// Renders custom Mermaid content for the block.
    /// This is called after the standard block node and connections are rendered.
    /// </summary>
    /// <param name="sb">The string builder to append Mermaid content to</param>
    /// <param name="block">The block definition to render</param>
    /// <param name="direction">The flowchart direction (LR, RL, TB, BT)</param>
    /// <param name="serviceProvider">Optional service provider for rendering dynamic content</param>
    void RenderCustomContent(StringBuilder sb, BlockDefinition block, string direction, IServiceProvider? serviceProvider);
}
