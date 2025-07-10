namespace Uniun.DataFlow.Blocks.PersistenRouting;
using System;
using Uniun.DataFlow.Blocks.Routing;

/// <summary>
/// Options for the PersistentRoutingBlock - simplified without expiration settings.
/// </summary>
/// <typeparam name="T"></typeparam>
public class PersistentRoutingBlockOptions<T> : BlockOptions
{
    /// <summary>
    /// A function used to select the routing key for an item.
    /// </summary>
    public Func<T, string> RoutingKeySelector { get; set; }

    /// <summary>
    /// A function used to resolve the route for a routing key.
    /// </summary>
    public Func<RoutingContext<T>, (IDataFlow DataFlow, ITargetBlock<T> TargetBlock)> RouteResolver { get; set; }
}
