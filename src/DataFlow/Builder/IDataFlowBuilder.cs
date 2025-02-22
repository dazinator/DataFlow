namespace Uniun.DataFlow.Builder;

public interface IDataFlowBuilder
{
    /// <summary>
    ///  Because flow building / creation can happen dynamically runtime, we need to be able to resolve servicesto utilise in those flows during the building of them.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
    public Dictionary<string, IBlock> Blocks { get; }
}
