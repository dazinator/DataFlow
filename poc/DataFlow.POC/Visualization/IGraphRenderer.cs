namespace DataFlow.POC.Visualization;

using DataFlow.POC.Core;

/// <summary>
/// Interface for rendering dataflow graphs to text-based diagram formats.
/// </summary>
public interface IGraphRenderer
{
    /// <summary>
    /// Renders the dataflow graph to a text-based diagram format.
    /// </summary>
    /// <param name="graph">The dataflow graph to render</param>
    /// <param name="options">Optional rendering options</param>
    /// <returns>The diagram as a string</returns>
    string Render(DataFlowGraph graph, GraphRenderOptions? options = null);
}
