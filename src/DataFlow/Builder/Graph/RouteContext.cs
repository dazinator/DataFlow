namespace Uniun.DataFlow.Builder.Graph;

using Uniun.DataFlow.Blocks;

/// <summary>
/// Context provided when building a route's sub-dataflow.
/// Contains information about the route being created and the item that triggered it.
/// </summary>
public class RouteContext
{
    /// <summary>
    /// The name of the route being created.
    /// For dynamic routes, this may differ from the registered template route name.
    /// </summary>
    public required string RouteName { get; init; }

    /// <summary>
    /// The registered route definition name that was used to create this route.
    /// For static routes, this equals RouteName. For dynamic routes, this is the template name.
    /// </summary>
    public required string RouteDefinitionName { get; init; }

    /// <summary>
    /// The item that triggered this route creation (for initialization purposes).
    /// </summary>
    public object? TriggeringItem { get; init; }

    /// <summary>
    /// The service provider scoped to this route.
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }

    /// <summary>
    /// A builder that can be used to construct the sub-dataflow for this route.
    /// </summary>
    public required IRouteBuilder RouteBuilder { get; init; }

    /// <summary>
    /// Indicates whether this route is being built at design-time (e.g., for diagram/visualization purposes).
    /// When true, the route factory should avoid operations that require actual data items.
    /// TriggeringItem may be null when IsDesignTime is true.
    /// </summary>
    public bool IsDesignTime { get; init; }
}
