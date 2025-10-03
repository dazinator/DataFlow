namespace Uniun.DataFlow.Builder;

public interface IDataFlowBuilder
{
    /// <summary>
    /// The shared state for this builder, containing blocks and other mutable state.
    /// </summary>
    public DataFlowBuilderState State { get; }
    
    /// <summary>
    ///  Because flow building / creation can happen dynamically runtime, we need to be able to resolve servicesto utilise in those flows during the building of them.
    /// </summary>
    public IServiceProvider ServiceProvider => State.ServiceProvider;
    
    /// <summary>
    /// Dictionary of all blocks in the dataflow, keyed by name.
    /// </summary>
    public Dictionary<string, IBlock> Blocks => State.Blocks;

   // public IBoundedChannelFactory BoundedChannelFactory { get; }
}
