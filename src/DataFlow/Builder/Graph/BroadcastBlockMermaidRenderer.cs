namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Mermaid renderer for broadcast blocks.
/// Adds a simple comment to indicate the block broadcasts to multiple targets.
/// The actual connections are already shown in the standard diagram rendering.
/// </summary>
public class BroadcastBlockMermaidRenderer : IMermaidBlockRenderer
{
    /// <summary>
    /// Determines if this renderer can handle the given block definition.
    /// </summary>
    public bool CanRender(BlockDefinition block)
    {
        return block.Metadata.TryGetValue("BlockType", out var blockTypeObj) &&
               blockTypeObj is string blockType && blockType == "Broadcast";
    }

    /// <summary>
    /// Renders a simple comment for broadcast blocks.
    /// The connections to targets are already shown via normal graph edges.
    /// </summary>
    public void RenderCustomContent(IBlockRenderContext context)
    {
        // Add a simple comment to indicate this is a broadcast block
        context.AppendLine($"%% '{context.Block.Name}' broadcasts to multiple targets");
    }
}
