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
    /// Adds a router that routes incoming items based on a selector key, to a target block that is lazily created by the provided block factory.
    /// The route is disposed after a period of inactivity as specified by the options. If the routing key is then seen again in the data stream, the route is re-created.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="routingKeySelector"></param>
    /// <param name="blockFactory"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static ITargetBlockBuilder<T> AddRouter<T>(
        this IDataFlowBuilder builder,
        string name,
        Func<T, string> routingKeySelector,
        Func<RoutingContext<T>, ITargetBlock<T>> blockFactory,
        Action<RoutingOptions>? configureOptions = null)
    {
        var options = new RoutingOptions();
        configureOptions?.Invoke(options);

        var block = new RoutingBlock<T>(
            routingKeySelector,
            blockFactory,
            options,
            builder.ServiceProvider.GetRequiredService<ILogger<RoutingBlock<T>>>());

        return builder.AddTargetBlock(name, block);
    }
}

