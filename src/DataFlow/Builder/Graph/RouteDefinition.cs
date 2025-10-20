namespace Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Represents a route definition in the routing graph.
/// This describes how to build a branch for a specific route name.
/// </summary>
public class RouteDefinition
{
    public RouteDefinition(string name, Type itemType, Func<RouteContext, IBranchBuilder> factory)
    {
        Name = name;
        ItemType = itemType;
        Factory = factory;
    }

    /// <summary>
    /// The name of this route (e.g., "template", "even", "odd").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The type of items that will be routed through this route.
    /// </summary>
    public Type ItemType { get; }

    /// <summary>
    /// Factory function to create the route's branch.
    /// Called when the route needs to be instantiated.
    /// The factory can either create a new branch or lookup and return an existing branch from the graph.
    /// </summary>
    public Func<RouteContext, IBranchBuilder> Factory { get; }

    /// <summary>
    /// Additional metadata about the route.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
