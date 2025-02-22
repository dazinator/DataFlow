namespace Uniun.DataFlow.Blocks.Routing;
using System;

public class RoutingContext<TItem>
{
    public string RoutingKey { get; set; }
    public TItem? Item { get; set; }

    public IServiceProvider ServiceProvider { get; set; }
    // public bool CompleteBlockOnRouteExpiry { get; internal set; } = true;
}

