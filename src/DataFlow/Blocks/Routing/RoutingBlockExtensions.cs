#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Uniun.DataFlow.Blocks;
#pragma warning restore IDE0130 // Namespace does not match folder structure
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Blocks.Routing;

public static class RoutingBlockExtensions
{   

    /// <summary>
    /// Adds a router that routes items based on a key selector. You provide a method to lazily build the sub data flow for a routing key. The route / sub data flow is disposed after a period of inactivity as specified by the options.
    /// If the routing key is then seen again in the data stream, the route is re-created.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="routingKeySelector"></param>
    /// <param name="routeResolver"></param>
    /// <param name="configureOptions"></param>
    /// <returns></returns>
    public static ITargetBlockBuilder<T> AddRouter<T>(
    this IDataFlowBuilder builder,
    string name,
    Func<T, string> routingKeySelector,
    Func<RoutingContext<T>, (DataFlow DataFlow, ITargetBlock<T> TargetBlock)> routeResolver,
    Action<RoutingBlockOptions<T>>? configureOptions = null)
    {
        return AddRouter<T>(builder, name, (options) =>
        {
            options.RoutingKeySelector = routingKeySelector;
            options.RouteResolver = routeResolver;
            configureOptions?.Invoke(options);
        });           
    }

    /// <summary>
    /// Adds a router block.
    /// </summary>
    public static ITargetBlockBuilder<T> AddRouter<T>(
        this IDataFlowBuilder builder,
        string name,
        Action<RoutingBlockOptions<T>> configureOptions
    )
    {
        var options = new RoutingBlockOptions<T>();
        configureOptions?.Invoke(options);
        //  configureOptions?.Invoke(options);
        var block = ActivatorUtilities.CreateInstance<RoutingBlock<T>>(builder.ServiceProvider, name, options);
        return builder.AddTargetBlock(name, block);
    }

}

