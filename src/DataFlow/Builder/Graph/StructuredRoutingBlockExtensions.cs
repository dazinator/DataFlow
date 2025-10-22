namespace Uniun.DataFlow.Builder.Graph;

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Blocks.Routing;

/// <summary>
/// Extension methods for adding routing blocks to the structured dataflow builder.
/// </summary>
public static class StructuredRoutingBlockExtensions
{
    /// <summary>
    /// Adds a routing block to the structured dataflow.
    /// </summary>
    /// <typeparam name="T">The type of items being routed</typeparam>
    /// <param name="builder">The dataflow builder</param>
    /// <param name="name">The name of the routing block</param>
    /// <param name="routeSelector">Function that selects the route name for each item</param>
    /// <param name="configureOptions">Optional configuration for routing options</param>
    /// <returns>A builder for configuring the routing block</returns>
    public static StructuredRoutingBlockBuilder<T> AddRouter<T>(
        this IStructuredDataFlowBuilder builder,
        string name,
        Func<T, string> routeSelector,
        Action<StructuredRoutingBlockOptions<T>>? configureOptions = null)
    {
        var options = new StructuredRoutingBlockOptions<T>
        {
            RouteSelector = routeSelector
        };

        configureOptions?.Invoke(options);

        // Store routing metadata in the block definition
        var metadata = new Dictionary<string, object>
        {
            ["IsRoutingBlock"] = true,
            ["RouteDefinitions"] = options.Routes
        };

        builder.AddBlockDefinition(
            name,
            sp =>
            {
                var logger = sp.GetRequiredService<ILogger<StructuredRoutingBlock<T>>>();
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();

                // Get the merge target block if configured
                ITargetBlock<T>? mergeTargetBlock = null;
                if (!string.IsNullOrEmpty(options.MergeIntoBlockName))
                {
                    // The merge target will be wired up during the Build phase
                    // For now, we just note it in the options
                }

                return new StructuredRoutingBlock<T>(
                    name,
                    logger,
                    scopeFactory,
                    channelFactory,
                    options,
                    builder.Graph,
                    mergeTargetBlock);
            },
            inputType: typeof(T),
            outputType: null,
            metadata: metadata);

        return new StructuredRoutingBlockBuilder<T>(builder, name, options);
    }
}

/// <summary>
/// Builder for configuring a routing block in the structured dataflow.
/// </summary>
public class StructuredRoutingBlockBuilder<T>
{
    private readonly IStructuredDataFlowBuilder _builder;
    private readonly string _blockName;
    private readonly StructuredRoutingBlockOptions<T> _options;

    public StructuredRoutingBlockBuilder(
        IStructuredDataFlowBuilder builder,
        string blockName,
        StructuredRoutingBlockOptions<T> options)
    {
        _builder = builder;
        _blockName = blockName;
        _options = options;
    }

    /// <summary>
    /// Registers a route with the routing block.
    /// The factory function now returns an IBranchBuilder instead of IDataFlow.
    /// </summary>
    /// <param name="routeName">The name of the route</param>
    /// <param name="routeFactory">Factory function to build the route's branch</param>
    /// <returns>This builder for chaining</returns>
    public StructuredRoutingBlockBuilder<T> RegisterRoute(
        string routeName,
        Func<RouteContext, IBranchBuilder> routeFactory)
    {
        if (_options.Routes.ContainsKey(routeName))
        {
            throw new InvalidOperationException($"Route '{routeName}' is already registered");
        }

        var routeDefinition = new RouteDefinition(routeName, typeof(T), routeFactory);
        _options.Routes[routeName] = routeDefinition;

        // Also add to the parent graph
        _builder.Graph.AddRouteDefinition(routeDefinition);

        return this;
    }

    /// <summary>
    /// Enables dynamic routing using the specified template route.
    /// When an item is routed to an unknown route name, a new route will be created
    /// using the template route definition.
    /// </summary>
    /// <param name="templateRouteName">The name of the route to use as a template for dynamic routes</param>
    /// <param name="maxDynamicRoutes">Optional maximum number of dynamic routes. Null means unlimited.</param>
    /// <returns>This builder for chaining</returns>
    public StructuredRoutingBlockBuilder<T> WithDynamicRouting(
        string templateRouteName,
        int? maxDynamicRoutes = null)
    {
        if (!_options.Routes.ContainsKey(templateRouteName))
        {
            throw new InvalidOperationException(
                $"Template route '{templateRouteName}' must be registered before enabling dynamic routing");
        }

        // Update the options that are already being used by the block
        _options.DynamicRouteTemplateName = templateRouteName;
        _options.MaxDynamicRoutes = maxDynamicRoutes;

        return this;
    }

    /// <summary>
    /// Configures the routing block to merge all route outputs into a dynamically created merge block.
    /// This automatically adds a specialized DynamicMergeBlock that can handle sources being added
    /// after execution starts (unlike BufferBlock).
    /// Use this overload when routes output the same type as the routing block input.
    /// </summary>
    /// <param name="mergeBlockName">The name for the merge block that will be created</param>
    /// <returns>This builder for chaining</returns>
    public StructuredRoutingBlockBuilder<T> MergeInto(string mergeBlockName)
    {
        return MergeInto<T>(mergeBlockName);
    }

    /// <summary>
    /// Configures the routing block to merge all route outputs into a dynamically created merge block.
    /// This automatically adds a specialized DynamicMergeBlock that can handle sources being added
    /// after execution starts (unlike BufferBlock).
    /// Use this overload when routes transform data to a different output type.
    /// </summary>
    /// <typeparam name="TOutput">The output type of the routes (may differ from T if routes transform data)</typeparam>
    /// <param name="mergeBlockName">The name for the merge block that will be created</param>
    /// <returns>This builder for chaining</returns>
    public StructuredRoutingBlockBuilder<T> MergeInto<TOutput>(string mergeBlockName)
    {
        _options.MergeIntoBlockName = mergeBlockName;
        
        // Automatically add a DynamicMergeBlock to handle the merging
        // This block supports sources being added dynamically after execution starts
        _builder.AddBlockDefinition(
            mergeBlockName,
            sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DynamicMergeBlock<TOutput>>>();
                var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();
                return new DynamicMergeBlock<TOutput>(
                    mergeBlockName,
                    logger,
                    channelFactory,
                    new BlockOptions { Capacity = 1000 });
            },
            inputType: typeof(TOutput),
            outputType: typeof(TOutput),
            metadata: new Dictionary<string, object>
            {
                ["IsDynamicMergeBlock"] = true,
                ["CreatedByRoutingBlock"] = _blockName
            });
        
        return this;
    }

    /// <summary>
    /// Configures this routing block to receive data from another block.
    /// </summary>
    /// <param name="sourceBlockName">The name of the source block</param>
    /// <returns>This builder for chaining</returns>
    public StructuredRoutingBlockBuilder<T> ReceiveFrom(string sourceBlockName)
    {
        _builder.AddConnection(sourceBlockName, _blockName, typeof(T));
        return this;
    }
}
