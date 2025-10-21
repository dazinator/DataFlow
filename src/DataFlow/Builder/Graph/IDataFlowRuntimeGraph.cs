namespace Uniun.DataFlow.Builder.Graph;

using Uniun.DataFlow.Blocks;

/// <summary>
/// Represents the runtime graph of a dataflow containing instantiated block instances.
/// This provides read-only access to the block instances that have been created during the Build phase.
/// </summary>
public interface IDataFlowRuntimeGraph
{
    /// <summary>
    /// Gets the name of the dataflow.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the build-time graph definition.
    /// </summary>
    DataFlowGraph Graph { get; }

    /// <summary>
    /// Gets all block instances in the runtime graph, keyed by block name.
    /// </summary>
    IReadOnlyDictionary<string, IBlock> BlockInstances { get; }

    /// <summary>
    /// Gets a block instance by name.
    /// </summary>
    /// <param name="name">The name of the block</param>
    /// <returns>The block instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if the block does not exist</exception>
    IBlock GetBlockInstance(string name);

    /// <summary>
    /// Tries to get a block instance by name.
    /// </summary>
    /// <param name="name">The name of the block</param>
    /// <param name="block">The block instance if found</param>
    /// <returns>True if the block was found, false otherwise</returns>
    bool TryGetBlockInstance(string name, out IBlock block);

    /// <summary>
    /// Gets a strongly-typed block instance by name.
    /// </summary>
    /// <typeparam name="TBlock">The type of block</typeparam>
    /// <param name="name">The name of the block</param>
    /// <returns>The block instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if the block does not exist or is not of the expected type</exception>
    TBlock GetBlockInstance<TBlock>(string name) where TBlock : IBlock;

    /// <summary>
    /// Tries to get a strongly-typed block instance by name.
    /// </summary>
    /// <typeparam name="TBlock">The type of block</typeparam>
    /// <param name="name">The name of the block</param>
    /// <param name="block">The block instance if found</param>
    /// <returns>True if the block was found and is of the expected type, false otherwise</returns>
    bool TryGetBlockInstance<TBlock>(string name, out TBlock block) where TBlock : IBlock;
}
