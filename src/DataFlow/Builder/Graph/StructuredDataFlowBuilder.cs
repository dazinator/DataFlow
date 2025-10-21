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
    private string? _lastSourceBlockName;
    private readonly Dictionary<string, IBranchBuilder> _branches = new();
    private int _branchCounter = 0;

    public StructuredDataFlowBuilder(IServiceProvider serviceProvider, string name)
    {
        State = new DataFlowBuilderState(serviceProvider);
        Graph = new DataFlowGraph(name);
    }

    public DataFlowBuilderState State { get; }

    public IServiceProvider ServiceProvider => State.ServiceProvider;

    /// <summary>
    /// Gets the graph representation of the dataflow.
    /// This can be used to inspect the structure before building.
    /// </summary>
    public DataFlowGraph Graph { get; }

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

        Graph.AddBlockDefinition(blockDefinition);
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

        Graph.AddConnection(connection);
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
    /// Adds a branch to the dataflow.
    /// A branch allows building independent sub-flows that share the same graph
    /// but maintain separate "last block" state for chaining.
    /// </summary>
    /// <param name="branchName">Optional unique name for the branch. If not specified, auto-generates name based on flow name, current block, and index.</param>
    /// <param name="startFromBlock">Optional block name to start the branch from. If not specified, uses current last source block.</param>
    /// <returns>A branch builder for building the branch</returns>
    public IBranchBuilder AddBranch(string? branchName = null, string? startFromBlock = null)
    {
        // Auto-generate branch name if not provided
        if (string.IsNullOrEmpty(branchName))
        {
            var currentBlock = startFromBlock ?? _lastSourceBlockName ?? "root";
            branchName = $"{Graph.Name}-{currentBlock}-branch-{_branchCounter}";
            _branchCounter++;
        }

        if (_branches.ContainsKey(branchName))
        {
            throw new ArgumentException($"Branch with name '{branchName}' already exists", nameof(branchName));
        }

        // Capture the current block that this branch is created from
        var branchStartBlock = startFromBlock ?? _lastSourceBlockName;
        var branch = new BranchBuilder(this, branchName, branchStartBlock, _branchCounter - 1);
        _branches[branchName] = branch;

        return branch;
    }

    /// <summary>
    /// Gets a previously created branch by name.
    /// </summary>
    /// <param name="branchName">Name of the branch to retrieve</param>
    /// <returns>The branch builder</returns>
    public IBranchBuilder GetBranch(string branchName)
    {
        if (!_branches.TryGetValue(branchName, out var branch))
        {
            throw new InvalidOperationException($"Branch '{branchName}' not found");
        }

        return branch;
    }

    /// <summary>
    /// Gets all branches that have been created.
    /// </summary>
    public IReadOnlyDictionary<string, IBranchBuilder> GetBranches() => _branches;

    /// <summary>
    /// Builds the dataflow asynchronously by instantiating all blocks and wiring them according to the graph.
    /// This implements a two-phase build process:
    /// Phase 1: Instantiate blocks and wire connections
    /// Phase 2: Initialize blocks in topological order
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the build process</param>
    public async Task<IDataFlow> Build(CancellationToken cancellationToken = default)
    {
        // Get logger for validation warnings
        var loggerFactory = ServiceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger(typeof(DataFlowGraph).FullName ?? "DataFlowGraph");

        // Validate the graph before building
        Graph.Validate(logger);

        // Phase 1: Instantiate all blocks and wire connections
        var blocks = new Dictionary<string, IBlock>();
        foreach (var definition in Graph.BlockDefinitions.Values)
        {
            var block = definition.Factory(ServiceProvider);
            blocks[definition.Name] = block;
            State.Blocks[definition.Name] = block;
        }

        // Wire up connections
        foreach (var connection in Graph.Connections)
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

        // Create the runtime graph
        var runtimeGraph = new DataFlowRuntimeGraph(Graph.Name, Graph, blocks);

        // Phase 2: Initialize blocks in topological order
        var iterator = new DataFlowGraphIterator(Graph);
        var blocksInOrder = iterator.IterateTopologically();

        foreach (var blockDefinition in blocksInOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var block = blocks[blockDefinition.Name];
            if (block is IDataFlowInitializable initializableBlock)
            {
                await initializableBlock.OnDataFlowInitializedAsync(runtimeGraph, cancellationToken);
            }
        }

        // Create and return the dataflow
        var metrics = ServiceProvider.GetRequiredService<Uniun.DataFlow.Metrics.IDataFlowMetrics>();
        return new DataFlow(Graph.Name, blocks.Values.ToList(), metrics, Graph);
    }
}
