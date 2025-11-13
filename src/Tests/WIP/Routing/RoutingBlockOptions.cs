namespace Uniun.DataFlow.Blocks.Routing;
using System;
using Microsoft.Extensions.Caching.Memory;
using Uniun.DataFlow;

public class RoutingBlockOptions<T> : BlockOptions
{
    /// <summary>
    /// A function used to select the routing key for an item.
    /// </summary>
    public Func<T, string>? RoutingKeySelector { get; set; }

    /// <summary>
    /// A function used to resolve the route for a routing key.
    /// </summary>
    public Func<RoutingContext<T>, (IDataFlow DataFlow, ITargetBlock<T> TargetBlock)>? RouteResolver { get; set; }

    /// <summary>
    /// The cache used to store routes. If not provided, an error will be thrown.
    /// </summary>   
    public IMemoryCache? RouteCache { get; set; }

    /// <summary>
    /// How long to keep a route alive after its last use. The route will then be drained and disposed. If encountered again it will be re-created.
    /// </summary>
    public TimeSpan RouteExpiration { get; set; } = TimeSpan.FromMinutes(5);


}

