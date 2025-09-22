#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Uniun.DataFlow.Blocks;
#pragma warning restore IDE0130 // Namespace does not match folder structure
using System;
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Blocks.Routing;

/// <summary>
/// Extension methods for adding the PersistentRoutingBlock to a DataFlow builder.
/// </summary>
public static class PersistentRoutingBlockExtensions
{
    /// <summary>
    /// Adds a persistent router that routes items based on a key selector.
    /// Routes are created dynamically but persist for the lifetime of the block.
    /// </summary>
    public static ITargetBlockBuilder<T> AddPersistentRouter<T>(
        this IDataFlowBuilder builder,
        string name,
        Func<T, string> routingKeySelector,
        Func<RoutingContext<T>, (IDataFlow DataFlow, ITargetBlock<T> TargetBlock)> routeResolver,
        Action<PersistentRoutingBlockOptions<T>>? configureOptions = null)
    {
        return AddPersistentRouter<T>(builder, name, options =>
        {
            options.RoutingKeySelector = routingKeySelector;
            options.RouteResolver = routeResolver;
            configureOptions?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds a persistent router block.
    /// </summary>
    public static ITargetBlockBuilder<T> AddPersistentRouter<T>(
        this IDataFlowBuilder builder,
        string name,
        Action<PersistentRoutingBlockOptions<T>> configureOptions)
    {
        var options = new PersistentRoutingBlockOptions<T>();
        configureOptions?.Invoke(options);

        var block = ActivatorUtilities.CreateInstance<PersistentRoutingBlock<T>>(builder.ServiceProvider, name, options);
        return builder.AddTargetBlock(name, block);
    }
}
