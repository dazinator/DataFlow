namespace Uniun.DataFlow.Blocks.Routing;
using System;
using Microsoft.Extensions.Caching.Memory;


public class RoutingOptions : BlockOptions
{
    /// <summary>
    /// The cache used to store routes. If not provided, an error will be thrown.
    /// </summary>   
    public IMemoryCache RouteCache { get; set; }

    /// <summary>
    /// How long to keep a route alive after its last use. The route will then be drained and disposed. If encountered again it will be re-created.
    /// </summary>
    public TimeSpan RouteExpiration { get; set; } = TimeSpan.FromMinutes(5);
}

