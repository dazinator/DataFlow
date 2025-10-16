namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Context provided to block renderers with callback methods for composing diagram content.
/// This allows outer renderers to intercept and augment content from inner renderers.
/// </summary>
public interface IBlockRenderContext
{
    /// <summary>
    /// The block definition being rendered.
    /// </summary>
    BlockDefinition Block { get; }

    /// <summary>
    /// The flowchart direction (LR, RL, TB, BT).
    /// </summary>
    string Direction { get; }

    /// <summary>
    /// Optional service provider for rendering dynamic content.
    /// </summary>
    IServiceProvider? ServiceProvider { get; }

    /// <summary>
    /// The current indentation level for rendering.
    /// </summary>
    string Indentation { get; }

    /// <summary>
    /// Appends a line of Mermaid content.
    /// This allows the outer renderer to intercept and modify the content.
    /// </summary>
    /// <param name="content">The content to append</param>
    void AppendLine(string content);

    /// <summary>
    /// Creates a nested rendering context with additional indentation.
    /// </summary>
    /// <param name="additionalIndent">Additional indentation to add</param>
    /// <returns>A new context with increased indentation</returns>
    IBlockRenderContext CreateNested(string additionalIndent = "    ");
}
