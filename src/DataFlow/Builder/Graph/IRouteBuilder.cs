namespace Uniun.DataFlow.Builder.Graph;

using Uniun.DataFlow.Blocks;

/// <summary>
/// A specialized builder for constructing route sub-dataflows.
/// Extends IBranchBuilder to be compatible with the branch-based architecture.
/// Routes must have a target block as their entry point (where routed items are sent).
/// </summary>
public interface IRouteBuilder : IBranchBuilder
{
    /// <summary>
    /// Gets the route name for this builder.
    /// </summary>
    string RouteName { get; }

    /// <summary>
    /// Sets the entry target block for this route.
    /// This is where routed items will be sent.
    /// </summary>
    void SetEntryBlock(string blockName);

    /// <summary>
    /// Gets the entry target block name for this route.
    /// </summary>
    string? GetEntryBlockName();

    /// <summary>
    /// Builds the route sub-dataflow.
    /// </summary>
    IDataFlow Build();

    /// <summary>
    /// Gets a target block from the route by name.
    /// </summary>
    ITargetBlock<T> GetTargetBlock<T>(string blockName);
}
