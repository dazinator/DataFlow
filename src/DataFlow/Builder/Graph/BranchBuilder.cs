namespace Uniun.DataFlow.Builder.Graph;

using Uniun.DataFlow.Blocks;

/// <summary>
/// A branch builder that operates within the context of a parent builder.
/// This allows building independent branches that share the same graph but maintain
/// separate "last block" state for chaining.
/// </summary>
public class BranchBuilder : IBranchBuilder
{
    private string? _lastSourceBlockInBranch;
    private readonly string? _parentBlockAtCreation;

    public BranchBuilder(IStructuredDataFlowBuilder parentBuilder, string branchName, string? initialSourceBlock = null, int? branchIndex = null)
    {
        ParentBuilder = parentBuilder;
        BranchName = branchName;
        BranchIndex = branchIndex;
        _parentBlockAtCreation = initialSourceBlock;
        _lastSourceBlockInBranch = initialSourceBlock ?? parentBuilder.GetLastSourceBlockName();
    }

    public string BranchName { get; }

    public int? BranchIndex { get; }

    public IStructuredDataFlowBuilder ParentBuilder { get; }

    public string? LastSourceBlockInBranch => _lastSourceBlockInBranch;
    
    public string? ParentBlockAtCreation => _parentBlockAtCreation;

    // Delegate graph operations to parent
    public DataFlowGraph Graph => ParentBuilder.Graph;

    public IServiceProvider ServiceProvider => ParentBuilder.ServiceProvider;

    public void AddBlockDefinition<TBlock>(
        string name,
        Func<IServiceProvider, TBlock> factory,
        Type? inputType = null,
        Type? outputType = null,
        Dictionary<string, object>? metadata = null) where TBlock : IBlock
    {
        // Add branch metadata to the block
        metadata ??= new Dictionary<string, object>();
        metadata["BranchName"] = BranchName;
        
        ParentBuilder.AddBlockDefinition(name, factory, inputType, outputType, metadata);
    }

    public void AddConnection(
        string sourceBlockName,
        string targetBlockName,
        Type? dataType = null,
        Dictionary<string, object>? metadata = null)
    {
        ParentBuilder.AddConnection(sourceBlockName, targetBlockName, dataType, metadata);
    }

    public void SetLastSourceBlock(string blockName)
    {
        _lastSourceBlockInBranch = blockName;
        // Don't update parent's last source block - that's the point of branches
    }

    public string? GetLastSourceBlockName()
    {
        return _lastSourceBlockInBranch;
    }
}
