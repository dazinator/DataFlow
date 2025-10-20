namespace Uniun.DataFlow.Blocks.Routing;

using Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Options for the StructuredRoutingBlock.
/// </summary>
public class StructuredRoutingBlockOptions<T> : BlockOptions
{
    /// <summary>
    /// A function that selects the route name for each item.
    /// </summary>
    public required Func<T, string> RouteSelector { get; init; }

    /// <summary>
    /// Routes registered with this routing block.
    /// Key is the route name, value is the route definition.
    /// </summary>
    public Dictionary<string, RouteDefinition> Routes { get; } = new();

    /// <summary>
    /// The name of the dynamic route template to use when creating new routes.
    /// If null, dynamic routing is disabled and unknown route names will throw an exception.
    /// </summary>
    public string? DynamicRouteTemplateName { get; set; }

    /// <summary>
    /// Maximum number of dynamic routes that can be created.
    /// Null means no limit. Only applies when DynamicRouteTemplateName is set.
    /// </summary>
    public int? MaxDynamicRoutes { get; set; }

    /// <summary>
    /// Optional name of a downstream target block to merge all route outputs into.
    /// When set, the routing block will automatically connect the last source block
    /// of each route to this target block, allowing routes to be merged.
    /// </summary>
    public string? MergeIntoBlockName { get; set; }
}
