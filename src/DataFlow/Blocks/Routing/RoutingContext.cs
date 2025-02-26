namespace Uniun.DataFlow.Blocks.Routing;
using System;

public class RoutingContext<TItem>
{
    public string RoutingKey { get; set; }
    public TItem? Item { get; set; }
    public IServiceProvider ServiceProvider { get; set; }
    /// <summary>
    /// The sub dataflow built to handle items for this route.
    /// </summary>
    public DataFlow DataFlow { get; set; }
    /// <summary>
    /// The target block in the sub dataflow that will receive items for this route.
    /// </summary>
    public ITargetBlock<TItem> TargetBlock { get; set; }
    // public bool CompleteBlockOnRouteExpiry { get; internal set; } = true;
}

