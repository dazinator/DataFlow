namespace Uniun.DataFlow.Blocks.Routing;

using System;
using System.Collections.Concurrent;
using System.Threading;
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
    private readonly ITargetBlock<T>? _mergeTargetBlock;

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
        ITargetBlock<T>? mergeTargetBlock = null) : base(name, options, logger)
    {
        _options = options;
        _scopeFactory = scopeFactory;
        _channelFactory = channelFactory;
        _logger = logger;
        _parentGraph = parentGraph;
        _mergeTargetBlock = mergeTargetBlock;
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

                // Get the last source block from the route for potential merging
                ISourceBlock<T>? lastSourceBlock = null;
                if (_mergeTargetBlock != null && !string.IsNullOrEmpty(routeBranch.GetLastSourceBlockName()))
                {
                    var lastBlockName = routeBranch.GetLastSourceBlockName();
                    var lastBlockDef = routeBuilder.Graph.GetBlockDefinition(lastBlockName!);
                    if (lastBlockDef.IsSourceBlock())
                    {
                        // Get the instantiated block from the state
                        var block = routeBuilder.Build(); // This will have already been built above
                        // We need to track blocks differently - for now, skip merge support in this iteration
                        _logger.LogWarning("Route merging will be implemented in a follow-up iteration");
                    }
                }

                var routeInstance = new RouteInstance<T>(
                    _logger,
                    routeName,
                    dataFlow,
                    targetBlock,
                    channelBlock,
                    routeScope);

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

        try
        {
            await Task.WhenAll(executionTasks);
            _logger.LogInformation("All routes finished execution successfully");
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
/// Represents an instantiated route with its dataflow and channels.
/// </summary>
internal class RouteInstance<T> : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly ITargetBlock<T> _targetBlock;
    private readonly IDataFlow _dataFlow;
    private readonly AsyncServiceScope _scope;

    public RouteInstance(
        ILogger logger,
        string routeName,
        IDataFlow dataFlow,
        ITargetBlock<T> targetBlock,
        InputChannelBlock<T> channelBlock,
        AsyncServiceScope scope)
    {
        _logger = logger;
        RouteName = routeName;
        _dataFlow = dataFlow;
        _targetBlock = targetBlock;
        ChannelBlock = channelBlock;
        _scope = scope;
    }

    public string RouteName { get; }
    public InputChannelBlock<T> ChannelBlock { get; }
    public Task ExecutionTask { get; private set; } = Task.CompletedTask;

    public void StartFlowExecution(IDataFlowContext context)
    {
        // Connect the channel to the target block
        _targetBlock.SetSource(ChannelBlock);

        // Start the dataflow execution
        ExecutionTask = _dataFlow.ExecuteAsync(context);
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
