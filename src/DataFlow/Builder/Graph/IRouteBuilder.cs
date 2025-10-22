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
    /// Gets the entry block for this route. This is the InputChannelBlock that receives items from the routing block.
    /// Available after Build() is called.
    /// </summary>
    IBlock? EntryBlock { get; }

    /// <summary>
    /// Ensures an InputChannelBlock exists at the start of the route's pipeline.
    /// The InputChannelBlock is connected to the route's entry target block.
    /// If an InputChannelBlock already exists in the correct position, does nothing.
    /// </summary>
    /// <typeparam name="T">The type of items that will be routed to this route</typeparam>
    /// <param name="channelFactory">Factory for creating the channel</param>
    /// <param name="options">Options for the InputChannelBlock</param>
    /// <returns>The name of the InputChannelBlock (either existing or newly created)</returns>
    string EnsureInputChannelBlock<T>(IBoundedChannelFactory channelFactory, BlockOptions options);

    /// <summary>
    /// Gets the InputChannelBlock for this route after it has been ensured.
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <returns>The InputChannelBlock instance, or null if not found</returns>
    Uniun.DataFlow.Blocks.InputChannel.InputChannelBlock<T>? GetInputChannelBlock<T>();

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

    /// <summary>
    /// Gets a source block from the route by name.
    /// This is used for connecting route outputs to merge targets.
    /// </summary>
    ISourceBlock<T> GetSourceBlock<T>(string blockName);

    /// <summary>
    /// Gets a block by name without type checking. Used for dynamic type scenarios like merge connections.
    /// </summary>
    IBlock GetBlock(string blockName);
}
