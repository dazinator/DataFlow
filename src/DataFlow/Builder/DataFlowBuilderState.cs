namespace Uniun.DataFlow.Builder;

using Uniun.DataFlow.Blocks;

/// <summary>
/// Holds the shared state for DataFlow builders, including blocks and the last added source block.
/// This state is passed between chained builder instances to maintain consistency.
/// </summary>
public class DataFlowBuilderState
{
    public DataFlowBuilderState(IServiceProvider serviceProvider)
    {
        Blocks = new Dictionary<string, IBlock>();
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// Dictionary of all blocks in the dataflow, keyed by name.
    /// </summary>
    public Dictionary<string, IBlock> Blocks { get; }

    /// <summary>
    /// The last added source block, used for positional chaining via ReceiveFromLast.
    /// </summary>
    public IBlock? LastSourceBlock { get; set; }

    /// <summary>
    /// Service provider for resolving dependencies during flow building.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
}
