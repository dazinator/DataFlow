namespace Uniun.DataFlow.Builder.Graph;

using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Blocks;

/// <summary>
/// A structured dataflow builder that creates a graph representation before building blocks.
/// This provides a two-phase building approach:
/// 1. Define phase: Build up metadata about blocks and connections
/// 2. Build phase: Instantiate blocks and wire them based on the graph
/// </summary>
public class StructuredDataFlowBuilder : IDataFlowBuilder, IStructuredDataFlowBuilder
{
    private readonly DataFlowGraph _graph;
    private string? _lastSourceBlockName;

    public StructuredDataFlowBuilder(IServiceProvider serviceProvider, string name)
    {
        State = new DataFlowBuilderState(serviceProvider);
        _graph = new DataFlowGraph(name);
    }

    public DataFlowBuilderState State { get; }

    public IServiceProvider ServiceProvider => State.ServiceProvider;

    /// <summary>
    /// Gets the graph representation of the dataflow.
    /// This can be used to inspect the structure before building.
    /// </summary>
    public DataFlowGraph Graph => _graph;

    /// <summary>
    /// Adds a block definition to the graph without instantiating it yet.
    /// </summary>
    public void AddBlockDefinition<TBlock>(
        string name, 
        Func<IServiceProvider, TBlock> factory,
        Type? inputType = null,
        Type? outputType = null,
        Dictionary<string, object>? metadata = null) where TBlock : IBlock
    {
        var blockDefinition = new BlockDefinition(
            name,
            typeof(TBlock),
            sp => factory(sp))
        {
            InputType = inputType,
            OutputType = outputType,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _graph.AddBlockDefinition(blockDefinition);
    }

    /// <summary>
    /// Adds a connection between two blocks in the graph.
    /// </summary>
    public void AddConnection(
        string sourceBlockName, 
        string targetBlockName, 
        Type? dataType = null,
        Dictionary<string, object>? metadata = null)
    {
        var connection = new BlockConnection(sourceBlockName, targetBlockName)
        {
            DataType = dataType,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _graph.AddConnection(connection);
    }

    /// <summary>
    /// Sets the last source block for positional chaining.
    /// </summary>
    public void SetLastSourceBlock(string blockName)
    {
        _lastSourceBlockName = blockName;
    }

    /// <summary>
    /// Gets the last source block name for positional chaining.
    /// </summary>
    public string? GetLastSourceBlockName() => _lastSourceBlockName;

    /// <summary>
    /// Builds the dataflow by instantiating all blocks and wiring them according to the graph.
    /// This is the second phase where we take the graph metadata and create the actual runtime flow.
    /// </summary>
    public IDataFlow Build()
    {
        // Get logger for validation warnings
        var loggerFactory = ServiceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger(typeof(DataFlowGraph).FullName ?? "DataFlowGraph");

        // Validate the graph before building
        _graph.Validate(logger);

        // Instantiate all blocks
        var blocks = new Dictionary<string, IBlock>();
        foreach (var definition in _graph.BlockDefinitions.Values)
        {
            var block = definition.Factory(ServiceProvider);
            blocks[definition.Name] = block;
            State.Blocks[definition.Name] = block;
        }

        // Wire up connections
        foreach (var connection in _graph.Connections)
        {
            var sourceBlock = blocks[connection.SourceBlockName];
            var targetBlock = blocks[connection.TargetBlockName];

            // Use reflection to call SetSource on the target block
            var setSourceMethod = targetBlock.GetType().GetMethod("SetSource");
            if (setSourceMethod != null)
            {
                setSourceMethod.Invoke(targetBlock, new object[] { sourceBlock });
            }
            else
            {
                throw new InvalidOperationException(
                    $"Target block '{connection.TargetBlockName}' does not have a SetSource method");
            }
        }

        // Create and return the dataflow
        var metrics = ServiceProvider.GetRequiredService<Uniun.DataFlow.Metrics.IDataFlowMetrics>();
        return new DataFlow(_graph.Name, blocks.Values.ToList(), metrics);
    }
}
