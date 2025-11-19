namespace DataFlow.POC.Tests.TestHelpers;

using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Helper methods for creating DataFlowGraphBuilder instances in tests with consistent patterns.
/// Migrates away from obsolete DataFlowGraphBuilder(string, ILogger?) constructor to DI-based pattern.
/// 
/// This helper consolidates 100+ graph instantiation call sites across tests into a unified,
/// maintainable pattern. It eliminates obsolete constructor warnings and provides a single place
/// to update if graph construction changes.
/// 
/// Benefits:
/// - Consistent pattern across all graph instantiation
/// - Encapsulation of DI setup for graph builders
/// - Single place to update if graph construction changes
/// - Improved test readability
/// - DRY principle - avoids repetitive service provider setup
/// 
/// Usage Examples:
/// 
/// // Simple graph builder (most common pattern)
/// var builder = GraphHelpers.CreateGraphBuilder("my-graph");
/// builder.AddBlock(producer)
///     .AddBlock(transformer)
///     .Connect(producer, transformer);
/// var graph = builder.Build();
/// 
/// // Graph builder with custom service provider
/// var serviceProvider = new ServiceCollection()
///     .AddLogging()
///     .BuildServiceProvider();
/// var builder = GraphHelpers.CreateGraphBuilder("my-graph", serviceProvider);
/// 
/// // Complete graph creation and execution helper
/// var graph = GraphHelpers.CreateGraph("my-graph", builder => {
///     builder.AddBlock(producer)
///         .AddBlock(transformer)
///         .Connect(producer, transformer);
/// });
/// await graph.ExecuteAsync(context);
/// </summary>
public static class GraphHelpers
{
    /// <summary>
    /// Creates a DataFlowGraphBuilder with a minimal service provider for testing.
    /// This is the primary helper for migrating from the obsolete constructor pattern.
    /// </summary>
    /// <param name="name">Name of the graph</param>
    /// <param name="serviceProvider">Optional custom service provider. If not provided, a minimal one is created.</param>
    /// <param name="logger">Optional logger for the graph</param>
    /// <returns>A DataFlowGraphBuilder instance using the DI pattern</returns>
    public static DataFlowGraphBuilder CreateGraphBuilder(
        string name,
        IServiceProvider? serviceProvider = null,
        ILogger<DataFlowGraph>? logger = null)
    {
        // Create minimal service provider if not provided
        serviceProvider ??= CreateMinimalServiceProvider();

        return new DataFlowGraphBuilder(name, serviceProvider, namespacePrefix: null, logger);
    }

    /// <summary>
    /// Creates and builds a complete DataFlowGraph using a configuration action.
    /// Useful for simple test scenarios where you want to configure and build in one step.
    /// </summary>
    /// <param name="name">Name of the graph</param>
    /// <param name="configure">Action to configure the graph builder</param>
    /// <param name="serviceProvider">Optional custom service provider</param>
    /// <returns>A built DataFlowGraph ready for execution</returns>
    public static DataFlowGraph CreateGraph(
        string name,
        Action<DataFlowGraphBuilder> configure,
        IServiceProvider? serviceProvider = null)
    {
        var builder = CreateGraphBuilder(name, serviceProvider);
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a minimal service provider for graph building.
    /// This is sufficient for most test scenarios that don't need DI resolution.
    /// </summary>
    private static IServiceProvider CreateMinimalServiceProvider()
    {
        var services = new ServiceCollection();
        // Add minimal required services if needed in the future
        return services.BuildServiceProvider();
    }
}
