namespace Uniun.DataFlow.Blocks.Routing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Blocks.InputChannel;

/// <summary>
/// A simplified routing block that creates routes dynamically but keeps them for the lifetime of the block.
/// Routes are not expired or disposed until the entire block completes execution.
/// </summary>
/// <typeparam name="T"></typeparam>
public class PersistentRoutingBlock<T> : BlockBase, ITargetBlock<T>
{
    private readonly Func<T, string> _routingKeySelector;
    private readonly Func<RoutingContext<T>, (IDataFlow DataFlow, ITargetBlock<T> TargetBlock)> _routeResolver;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBoundedChannelFactory _channelFactory;
    private readonly ILogger<PersistentRoutingBlock<T>> _logger;

    private ISourceBlock<T>? _source;

    // Thread-safe storage for established routes
    private readonly ConcurrentDictionary<string, PersistentRouteInfo<T>> _routes = new();

    // Semaphore to ensure only one thread can create a route for a given key
    private readonly SemaphoreSlim _routeCreationLock = new(1);

    public PersistentRoutingBlock(
        string name,
        ILogger<PersistentRoutingBlock<T>> logger,
        IServiceScopeFactory scopeFactory,
        IBoundedChannelFactory channelFactory,
        PersistentRoutingBlockOptions<T> options) : base(name, options, logger)
    {
        _routingKeySelector = options.RoutingKeySelector;
        _routeResolver = options.RouteResolver;
        _scopeFactory = scopeFactory;
        _channelFactory = channelFactory;
        _logger = logger;
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
            // Always try to cleanup, even if processing failed
            try
            {
                await CompleteAllRoutesAsync();
                // We wait for all dynamic routes to finish execution
                // because this block execution represents the composite of its routes.
                // Unless these are taken into consideration, the block would complete and the parent dataflow would not have awareness of these dynamically created routes so would
                // complete prematurely.
                await WaitForAllRoutesAsync();
            }
            catch (Exception cleanupEx)
            {
                // Log cleanup errors but don't throw them
                _logger.LogError(cleanupEx, "Error during cleanup");
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
            var routingKey = _routingKeySelector(item);

            // Get or create the route for this key
            var routeInfo = await GetOrCreateRouteAsync(context, routingKey, item, context.CancellationToken);

            // Write the item to the route's channel
            await routeInfo.ChannelBlock.WriteAsync(item, context.CancellationToken);
            RecordOperation();
        }
    }

    private async Task<PersistentRouteInfo<T>> GetOrCreateRouteAsync(
        IDataFlowContext context,
        string routingKey,
        T item,
        CancellationToken cancellationToken)
    {
        // Fast path: route already exists
        if (_routes.TryGetValue(routingKey, out var existingRoute))
        {
            return existingRoute;
        }

        // Slow path: need to create the route
        await _routeCreationLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_routes.TryGetValue(routingKey, out existingRoute))
            {
                return existingRoute;
            }

            _logger.LogDebug("Creating new route for key: {routingKey}", routingKey);

            // Create a new scope for this route
            var routeScope = _scopeFactory.CreateAsyncScope();

            var routingContext = new RoutingContext<T>
            {
                RoutingKey = routingKey,
                Item = item,
                ServiceProvider = routeScope.ServiceProvider
            };

            try
            {
                // Get both DataFlow and TargetBlock from resolver
                var (dataFlow, targetBlock) = _routeResolver(routingContext);

                routingContext.DataFlow = dataFlow;
                routingContext.TargetBlock = targetBlock;

                var logger = routeScope.ServiceProvider.GetRequiredService<ILogger<InputChannelBlock<T>>>();
                var channelBlock = new InputChannelBlock<T>($"{targetBlock.Name}-route-{routingKey}", logger, _channelFactory, Options);

                var routeInfo = new PersistentRouteInfo<T>(_logger, routingContext, channelBlock, routeScope);

                _logger.LogDebug("Starting DataFlow execution for route: {routingKey}", routingKey);
                routeInfo.StartFlowExecution(targetBlock, context);

                // Add to routes dictionary
                _routes[routingKey] = routeInfo;

                _logger.LogInformation("Route created and started for key: {routingKey}", routingKey);

                return routeInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating route for key: {routingKey}", routingKey);

                // Dispose the scope on failure
                await routeScope.DisposeAsync();

                //_exceptions.Add(ex);
                throw;
            }
        }
        finally
        {
            _routeCreationLock.Release();
        }
    }

    private Task CompleteAllRoutesAsync()
    {
        _logger.LogDebug("Completing all routes - signaling no more data");

        var completionTasks = new List<Task>();

        foreach (var route in _routes.Values)
        {
            try
            {
                route.Complete();
                _logger.LogDebug("Completed route: {routingKey}", route.Context.RoutingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing route: {routingKey}", route.Context.RoutingKey);

            }
        }

        _logger.LogInformation("All routes completed - {routeCount} routes", _routes.Count);
        return Task.CompletedTask;
    }

    private async Task WaitForAllRoutesAsync()
    {
        if (_routes.IsEmpty)
        {
            _logger.LogDebug("No routes to wait for");
            return;
        }

        _logger.LogDebug("Waiting for all routes to finish execution - {routeCount} routes", _routes.Count);

        var executionTasks = _routes.Values
            .Select(route => route.ExecutionTask)
            .OfType<Task>()
            .ToArray();

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
        foreach (var route in _routes.Values)
        {
            try
            {
                await route.DisposeAsync();
                _logger.LogDebug("Disposed route: {routingKey}", route.Context.RoutingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing route: {routingKey}", route.Context.RoutingKey);
            }
        }

        _logger.LogInformation("All routes disposed");
    }
}
