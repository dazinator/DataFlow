namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Represents a branch in a dataflow graph.
/// A branch is a sub-flow that starts from a common point (typically a broadcast block)
/// and can contain multiple blocks that execute independently.
/// </summary>
public interface IBranchBuilder : IStructuredDataFlowBuilder
{
    /// <summary>
    /// Gets the name of this branch.
    /// </summary>
    string BranchName { get; }

    /// <summary>
    /// Gets the index of this branch (when auto-generated), or null if explicitly named.
    /// </summary>
    int? BranchIndex { get; }

    /// <summary>
    /// Gets the parent builder that this branch was created from.
    /// </summary>
    IStructuredDataFlowBuilder ParentBuilder { get; }

    /// <summary>
    /// Gets the last source block in this branch for chaining.
    /// </summary>
    string? LastSourceBlockInBranch { get; }

    /// <summary>
    /// Gets the parent block where this branch was created from (captured at creation time).
    /// </summary>
    string? ParentBlockAtCreation { get; }
}
