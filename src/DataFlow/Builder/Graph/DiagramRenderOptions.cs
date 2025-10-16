namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Options for rendering dataflow graph diagrams.
/// </summary>
public class DiagramRenderOptions
{
    /// <summary>
    /// If true, collapses concurrent branches that follow the same pattern into a single visual representation.
    /// For example, instead of showing 100 individual processor blocks, it shows "processor [x100]".
    /// </summary>
    public bool CollapseConcurrentBranches { get; set; } = false;

    /// <summary>
    /// Maximum number of concurrent branches to show individually before collapsing.
    /// If the number of branches exceeds this threshold and CollapseConcurrentBranches is true,
    /// they will be collapsed into a single representation.
    /// </summary>
    public int MaxBranchesToShowIndividually { get; set; } = 5;

    /// <summary>
    /// When branches are collapsed, show this many example branches explicitly.
    /// For example, with ShowExampleBranches=2 and 10 total branches, it might show
    /// "branch-0, branch-1, ... branch-9 [x10]"
    /// </summary>
    public int ShowExampleBranches { get; set; } = 2;

    /// <summary>
    /// If true, renders branches within subgraphs (boxes) to visually group them together.
    /// This helps to identify which blocks belong to the same branch.
    /// Similar to how routing blocks render their routes in subgraphs.
    /// </summary>
    public bool GroupBranchesInSubgraphs { get; set; } = true;
}
