namespace Uniun.DataFlow.Blocks.Routing;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Blocks.InputChannel;
using Uniun.DataFlow.Builder.Graph;

/// <summary>
/// A routing block for the structured dataflow builder.
/// Supports both static (pre-registered) and dynamic routing.
/// Routes are created on-demand and persist for the lifetime of the block.
/// Routes are now branch-based and can optionally merge into a downstream block.
/// </summary>
public class StructuredRoutingBlock<T> : BlockBase, ITargetBlock<T>
{
    private readonly StructuredRoutingBlockOptions<T> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBoundedChannelFactory _channelFactory;
    private readonly ILogger<StructuredRoutingBlock<T>> _logger;
    private readonly DataFlowGraph _parentGraph;
    private IBlock? _mergeTargetBlock; // Store as IBlock to support any target type

    private ISourceBlock<T>? _source;

    // Thread-safe storage for instantiated routes
    private readonly ConcurrentDictionary<string, RouteInstance<T>> _routeInstances = new();

    // Semaphore to ensure only one thread can create a route at a time
    private readonly SemaphoreSlim _routeCreationLock = new(1);

    // Counter for dynamic routes created
    private int _dynamicRoutesCreated = 0;

    public StructuredRoutingBlock(
        string name,
        ILogger<StructuredRoutingBlock<T>> logger,
        IServiceScopeFactory scopeFactory,
        IBoundedChannelFactory channelFactory,
        StructuredRoutingBlockOptions<T> options,
        DataFlowGraph parentGraph,
        IBlock? mergeTargetBlock = null) : base(name, options, logger)
    {
        _options = options;
        _scopeFactory = scopeFactory;
        _channelFactory = channelFactory;
        _logger = logger;
        _parentGraph = parentGraph;
        _mergeTargetBlock = mergeTargetBlock;
    }

    /// <summary>
    /// Called after all blocks in the dataflow have been instantiated.
    /// This is where we resolve the merge target block if configured.
    /// The merge target can be any ITargetBlock regardless of its type parameter.
    /// </summary>
    public override void OnDataFlowInitialized(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_options.MergeIntoBlockName))
        {
            _logger.LogDebug("Resolving merge target block: {MergeTargetBlockName}", _options.MergeIntoBlockName);

            if (!runtimeGraph.TryGetBlockInstance(_options.MergeIntoBlockName, out var mergeTargetBlock))
            {
                throw new InvalidOperationException(
                    $"Merge target block '{_options.MergeIntoBlockName}' not found");
            }

            _mergeTargetBlock = mergeTargetBlock;
            _logger.LogInformation("Merge target block '{MergeTargetBlockName}' resolved successfully", _options.MergeIntoBlockName);
        }
    }

    public void SetSource(ISourceBlock<T> source)
    {
        _source = source;
    }

    private void EnsureSource()
    {
        if (_source is null)
        {
            throw new InvalidOperationException("No source block configured");
        }
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        EnsureSource();

        try
        {
            // Process all items from the source
            await ProcessSourceAsync(context);
        }
        finally
        {
            // Complete all routes and wait for them to finish
            try
            {
                await CompleteAllRoutesAsync();
                await WaitForAllRoutesAsync();
                
                // Signal the merge block that all routes are done
                if (_mergeTargetBlock is IDynamicMergeBlock mergeBlock)
                {
                    mergeBlock.Complete();
                    _logger.LogDebug("Signaled completion to merge block");
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Error during route cleanup");
            }
        }
    }

    private async Task ProcessSourceAsync(IDataFlowContext context)
    {
        await ExecuteParallelActivities(context, Options.MaxConcurrency, async (index, ctx) =>
        {
            await ExecuteStreamProcessorAsync(index, ctx);
        });
    }

    protected async Task ExecuteStreamProcessorAsync(int index, IDataFlowContext context)
    {
        await foreach (var item in _source!.GetAsyncEnumerable(this, context.CancellationToken))
        {
            var routeName = _options.RouteSelector(item);

            // Get or create the route instance for this name
            var routeInstance = await GetOrCreateRouteAsync(context, routeName, item, context.CancellationToken);

            // Write the item to the route's channel
            await routeInstance.ChannelBlock.WriteAsync(item, context.CancellationToken);
            RecordOperation();
        }
    }

    private async Task<RouteInstance<T>> GetOrCreateRouteAsync(
        IDataFlowContext context,
        string routeName,
        T item,
        CancellationToken cancellationToken)
    {
        // Fast path: route already exists
        if (_routeInstances.TryGetValue(routeName, out var existingInstance))
        {
            return existingInstance;
        }

        // Slow path: need to create the route
        await _routeCreationLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_routeInstances.TryGetValue(routeName, out existingInstance))
            {
                return existingInstance;
            }

            _logger.LogDebug("Creating new route for name: {routeName}", routeName);

            // Determine which route definition to use
            string routeDefinitionName;

            if (_options.Routes.TryGetValue(routeName, out var routeDefinition))
            {
                // Static route found
                routeDefinitionName = routeName;
                _logger.LogDebug("Using static route definition: {routeName}", routeName);
            }
            else if (!string.IsNullOrEmpty(_options.DynamicRouteTemplateName))
            {
                // Dynamic routing is enabled, use the template
                if (!_options.Routes.TryGetValue(_options.DynamicRouteTemplateName, out routeDefinition))
                {
                    throw new InvalidOperationException(
                        $"Dynamic route template '{_options.DynamicRouteTemplateName}' not found");
                }

                // Check dynamic route limit
                if (_options.MaxDynamicRoutes.HasValue)
                {
                    var currentCount = Interlocked.Increment(ref _dynamicRoutesCreated);
                    if (currentCount > _options.MaxDynamicRoutes.Value)
                    {
                        throw new InvalidOperationException(
                            $"Maximum dynamic routes limit ({_options.MaxDynamicRoutes.Value}) exceeded");
                    }
                }

                routeDefinitionName = _options.DynamicRouteTemplateName;
                _logger.LogDebug("Creating dynamic route '{routeName}' using template '{template}'",
                    routeName, _options.DynamicRouteTemplateName);
            }
            else
            {
                // No dynamic routing and route not found
                throw new InvalidOperationException(
                    $"Route '{routeName}' not found and dynamic routing is not enabled");
            }

            // Create a new scope for this route
            var routeScope = _scopeFactory.CreateAsyncScope();

            try
            {
                // Build the route using the new branch-based approach
                var routeBuilder = new RouteBuilder(routeScope.ServiceProvider, routeName, _parentGraph);

                var routeContext = new RouteContext
                {
                    RouteName = routeName,
                    RouteDefinitionName = routeDefinitionName,
                    TriggeringItem = item,
                    ServiceProvider = routeScope.ServiceProvider,
                    ParentGraph = _parentGraph,
                    RouteBuilder = routeBuilder,
                    IsDesignTime = false  // Runtime execution mode
                };

                // Call the factory to get the branch for this route
                var routeBranch = routeDefinition.Factory(routeContext);

                // Build the dataflow from the route branch
                var dataFlow = routeBuilder.Build();

                // Get the last source block if merge target is configured
                IBlock? lastSourceBlock = null;
                if (_mergeTargetBlock != null && !string.IsNullOrEmpty(routeBranch.GetLastSourceBlockName()))
                {
                    var lastBlockName = routeBranch.GetLastSourceBlockName();
                    try
                    {
                        lastSourceBlock = routeBuilder.GetBlock(lastBlockName!);
                        
                        // Register the source with the merge target via SetSource
                        // Note: DynamicMergeBlock doesn't actively use this in its implementation,
                        // but we call it to adhere to expected block lifecycle patterns
                        var setSourceMethod = _mergeTargetBlock.GetType().GetMethod(nameof(ITargetBlock<object>.SetSource));
                        if (setSourceMethod != null)
                        {
                            setSourceMethod.Invoke(_mergeTargetBlock, new object[] { lastSourceBlock });
                            _logger.LogDebug(
                                "Registered last source block '{LastBlockName}' of route '{RouteName}' with merge target",
                                lastBlockName, routeName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, 
                            "Failed to get last source block '{LastBlockName}' for merge on route '{RouteName}'",
                            lastBlockName, routeName);
                    }
                }

                // Get the entry block - use the first entry block from the graph
                var entryBlocks = routeBuilder.Graph.GetEntryBlocks().ToList();
                if (!entryBlocks.Any())
                {
                    throw new InvalidOperationException(
                        $"Route '{routeName}' must have at least one entry block. The first target block added is automatically marked as the entry block, or you can explicitly call AsEntry() on a target block.");
                }

                var entryBlockName = entryBlocks.First().Name;
                var targetBlock = routeBuilder.GetTargetBlock<T>(entryBlockName);

                // Create the input channel for this route with bounded capacity
                // Use capacity = MaxConcurrency to reduce memory usage and get backpressure
                var channelOptions = new BlockOptions
                {
                    Capacity = Math.Max(1, Options.MaxConcurrency),
                    MaxConcurrency = 1 // Channel itself doesn't need concurrency
                };

                var logger = routeScope.ServiceProvider.GetRequiredService<ILogger<InputChannelBlock<T>>>();
                var channelBlock = new InputChannelBlock<T>(
                    $"{targetBlock.Name}-route-{routeName}",
                    logger,
                    _channelFactory,
                    channelOptions);

                var routeInstance = new RouteInstance<T>(
                    _logger,
                    routeName,
                    dataFlow,
                    targetBlock,
                    channelBlock,
                    routeScope,
                    lastSourceBlock,
                    _mergeTargetBlock);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    // Log route graph statistics at debug level
                    var routeGraph = routeBuilder.Graph;
                    _logger.LogDebug(
                        "Starting DataFlow execution for route: {routeName} (definition: {routeDefinitionName}). Graph: {blockCount} blocks, {connectionCount} connections",
                        routeName, routeDefinitionName, routeGraph.BlockDefinitions.Count, routeGraph.Connections.Count);
                }
                else
                {
                    _logger.LogDebug("Starting DataFlow execution for route: {routeName} (definition: {routeDefinitionName})",
                        routeName, routeDefinitionName);
                }

                routeInstance.StartFlowExecution(context);

                // Add to routes dictionary
                _routeInstances[routeName] = routeInstance;

                _logger.LogInformation("Route created and started: {routeName}", routeName);

                return routeInstance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating route: {routeName}", routeName);

                // Dispose the scope on failure
                await routeScope.DisposeAsync();
                throw;
            }
        }
        finally
        {
            _routeCreationLock.Release();
        }
    }

    private async Task CompleteAllRoutesAsync()
    {
        _logger.LogDebug("Completing all routes - signaling no more data");

        foreach (var route in _routeInstances.Values)
        {
            try
            {
                route.Complete();
                _logger.LogDebug("Completed route: {routeName}", route.RouteName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing route: {routeName}", route.RouteName);
            }
        }

        _logger.LogInformation("All routes completed - {routeCount} routes", _routeInstances.Count);
    }

    private async Task WaitForAllRoutesAsync()
    {
        if (_routeInstances.IsEmpty)
        {
            _logger.LogDebug("No routes to wait for");
            return;
        }

        _logger.LogDebug("Waiting for all routes to finish execution - {routeCount} routes", _routeInstances.Count);

        var executionTasks = _routeInstances.Values.Select(route => route.ExecutionTask).ToArray();
        var mergeProducerTasks = _routeInstances.Values
            .Where(route => route.MergeProducerTask != null)
            .Select(route => route.MergeProducerTask!)
            .ToArray();

        try
        {
            await Task.WhenAll(executionTasks);
            _logger.LogInformation("All routes finished execution successfully");
            
            // Wait for all merge producer tasks to complete
            if (mergeProducerTasks.Length > 0)
            {
                await Task.WhenAll(mergeProducerTasks);
                _logger.LogInformation("All merge producer tasks completed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for routes to complete");
        }

        // Dispose all routes
        foreach (var route in _routeInstances.Values)
        {
            try
            {
                await route.DisposeAsync();
                _logger.LogDebug("Disposed route: {routeName}", route.RouteName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing route: {routeName}", route.RouteName);
            }
        }

        _logger.LogInformation("All routes disposed");
    }
}

/// <summary>
/// Helper class for pumping items from a route's source block to a merge target.
/// This class is instantiated via reflection with the correct item type.
/// </summary>
/// <typeparam name="TItem">The type of items being pumped</typeparam>
internal class MergeProducerHelper<TItem>
{
    private readonly ILogger _logger;

    public MergeProducerHelper(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Pumps items from a source block to a merge target's writer.
    /// This method uses strongly-typed code after being instantiated with the correct type via reflection.
    /// </summary>
    public async Task PumpToMergeTargetAsync(IBlock lastSourceBlock, IBlock mergeTarget, CancellationToken cancellationToken)
    {
        // Cast to strongly-typed interfaces (we know these are correct because we were instantiated with the right type)
        var sourceBlock = (ISourceBlock<TItem>)lastSourceBlock;
        var dynamicMergeBlock = (DynamicMergeBlock<TItem>)mergeTarget;

        _logger.LogDebug("MergeProducerHelper<{Type}>: Starting to pump items", typeof(TItem).Name);

        // Get items from source and write to merge target's writer
        await foreach (var item in sourceBlock.GetAsyncEnumerable(dynamicMergeBlock, cancellationToken).ConfigureAwait(false))
        {
            await dynamicMergeBlock.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogDebug("MergeProducerHelper<{Type}>: Completed", typeof(TItem).Name);
    }
}
internal class RouteInstance<T> : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly ITargetBlock<T> _targetBlock;
    private readonly IDataFlow _dataFlow;
    private readonly AsyncServiceScope _scope;
    private readonly IBlock? _lastSourceBlock;
    private readonly IBlock? _mergeTarget;
    private Task? _mergeProducerTask;

    public RouteInstance(
        ILogger logger,
        string routeName,
        IDataFlow dataFlow,
        ITargetBlock<T> targetBlock,
        InputChannelBlock<T> channelBlock,
        AsyncServiceScope scope,
        IBlock? lastSourceBlock = null,
        IBlock? mergeTarget = null)
    {
        _logger = logger;
        RouteName = routeName;
        _dataFlow = dataFlow;
        _targetBlock = targetBlock;
        ChannelBlock = channelBlock;
        _scope = scope;
        _lastSourceBlock = lastSourceBlock;
        _mergeTarget = mergeTarget;
    }

    public string RouteName { get; }
    public InputChannelBlock<T> ChannelBlock { get; }
    public Task ExecutionTask { get; private set; } = Task.CompletedTask;
    public Task? MergeProducerTask => _mergeProducerTask;

    public void StartFlowExecution(IDataFlowContext context)
    {
        // Connect the channel to the target block
        _targetBlock.SetSource(ChannelBlock);

        // Start the dataflow execution
        ExecutionTask = _dataFlow.ExecuteAsync(context);

        // If there's a last source block and merge target, start a producer task
        // to pump data from the route's output to the merge target
        if (_lastSourceBlock != null && _mergeTarget != null)
        {
            _mergeProducerTask = StartMergeProducerAsync(_lastSourceBlock, _mergeTarget, context.CancellationToken);
        }
    }

    private async Task StartMergeProducerAsync(IBlock lastSourceBlock, IBlock mergeTarget, CancellationToken cancellationToken)
    {
        try
        {
            // Use reflection to discover the output type of the route's last source block
            var sourceBlockType = lastSourceBlock.GetType()
                .GetInterfaces()
                .FirstOrDefault(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(ISourceBlock<>));

            if (sourceBlockType == null)
            {
                _logger.LogWarning("Route: Last source block does not implement ISourceBlock<>");
                return;
            }

            var itemType = sourceBlockType.GetGenericArguments()[0];
            _logger.LogDebug("Route: Starting merge producer for output type {Type}", itemType.Name);

            // Create a strongly-typed helper using reflection, then invoke it
            var helperType = typeof(MergeProducerHelper<>).MakeGenericType(itemType);
            var helper = Activator.CreateInstance(helperType, _logger);
            
            var pumpMethod = helperType.GetMethod("PumpToMergeTargetAsync");
            if (pumpMethod == null)
            {
                _logger.LogWarning("Route: Cannot find PumpToMergeTargetAsync method on helper");
                return;
            }

            var pumpTask = (Task)pumpMethod.Invoke(helper, new object[] { lastSourceBlock, mergeTarget, cancellationToken })!;
            await pumpTask.ConfigureAwait(false);

            _logger.LogDebug("Route: Merge producer completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Route: Merge producer cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Route: Error in merge producer");
            throw;
        }
    }

    public void Complete()
    {
        ChannelBlock.Complete();
    }

    public async ValueTask DisposeAsync()
    {
        // Warn if disposing while execution is still active
        if (ExecutionTask != null && !ExecutionTask.IsCompleted)
        {
            _logger.LogWarning(
                "Disposing route '{RouteName}' while its ExecutionTask is still active. " +
                "This may lead to unexpected behavior as the DI scope is being disposed while the dataflow is executing.",
                RouteName);
        }

        await _scope.DisposeAsync();
    }
}
