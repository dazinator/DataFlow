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
        // Use the obsolete constructor for benchmarking to keep benchmarks simple
        #pragma warning disable CS0618 // Type or member is obsolete
        return new DataFlowGraphBuilder(name, logger);
        #pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// ⚠️ DEPRECATED - Benchmark-only extension method for routing.
    /// Connects a router block to multiple filter blocks using broadcast.
    /// This is a workaround for the deprecated RouterBlock pattern.
    /// </summary>
    [Obsolete("ConnectRouted is deprecated. Use SelectiveRoutingEdgeStrategy for new code.")]
    public static DataFlowGraphBuilder ConnectRouted(
        this DataFlowGraphBuilder builder,
        IBlock router,
        Dictionary<string, IBlock> routeKeyToBlock,
        int bufferCapacity = 100)
    {
        // The router emits RoutedItem<T>, which needs to be broadcast to all filters
        // Each filter will filter for its specific route key
        var filters = routeKeyToBlock.Values.ToList();
        
        // Use broadcast strategy to send all routed items to all filters
        var edge = new Edge(
            router,
            filters,
            new BroadcastEdgeStrategy(BufferMode.Bounded, bufferCapacity));
        
        builder.AddEdge(edge);
        return builder;
    }
}

