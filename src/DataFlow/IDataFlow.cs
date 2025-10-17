// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

using Uniun.DataFlow.Builder.Graph;

public interface IDataFlow
{
    string Name { get; set; }
    
    /// <summary>
    /// Gets the graph representation of this dataflow.
    /// This can be used to inspect the structure or render diagrams.
    /// </summary>
    DataFlowGraph? Graph { get; }
    
    Task ExecuteAsync(IDataFlowContext context);
}
