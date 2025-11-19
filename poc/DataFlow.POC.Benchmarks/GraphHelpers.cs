namespace DataFlow.POC.Benchmarks;

using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Helper methods for creating DataFlowGraphBuilder instances in benchmarks.
/// Migrates away from obsolete DataFlowGraphBuilder(string, ILogger?) constructor.
/// </summary>
public static class GraphHelpers
{
    /// <summary>
    /// Creates a DataFlowGraphBuilder with a minimal service provider for benchmarking.
    /// </summary>
    public static DataFlowGraphBuilder CreateGraphBuilder(
        string name,
        IServiceProvider? serviceProvider = null,
        ILogger<DataFlowGraph>? logger = null)
    {
        serviceProvider ??= new ServiceCollection().BuildServiceProvider();
        return new DataFlowGraphBuilder(name, serviceProvider, namespacePrefix: null, logger);
    }
}
