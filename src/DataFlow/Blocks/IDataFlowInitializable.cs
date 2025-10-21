namespace Uniun.DataFlow.Blocks;

using Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Optional interface that blocks can implement to receive runtime initialization.
/// This is called during the Build phase after all blocks have been instantiated
/// but before the dataflow execution begins.
/// Blocks are initialized in topological order according to the DAG.
/// </summary>
public interface IDataFlowInitializable
{
    /// <summary>
    /// Called after all blocks in the dataflow have been instantiated.
    /// This allows blocks to discover and interact with other blocks in the flow.
    /// </summary>
    /// <param name="runtimeGraph">The runtime graph containing all instantiated blocks</param>
    /// <param name="cancellationToken">Cancellation token for the initialization process</param>
    /// <returns>A task representing the initialization operation</returns>
    Task OnDataFlowInitializedAsync(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken);
}
