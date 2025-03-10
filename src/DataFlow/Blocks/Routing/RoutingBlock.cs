namespace Uniun.DataFlow.Blocks.Routing;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks.InputChannel;

public class RoutingBlock<T> : BlockBase, ITargetBlock<T>
{
    private readonly Func<T, string> _routingKeySelector;
    private readonly Func<RoutingContext<T>, (DataFlow DataFlow, ITargetBlock<T> TargetBlock)> _routeResolver;
    private readonly IMemoryCache _routeCache;
    private readonly TimeSpan _routeExpiration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBoundedChannelFactory _channelFactory;
    private ISourceBlock<T>? _source;
    private readonly ILogger<RoutingBlock<T>> _logger;

    // Store active routes with their routing keys for tracking
    private readonly ConcurrentDictionary<string, RouteInfo<T>> _activeRoutes = new();

    // Channel to offload expired / finished routes for disposal in a controlled manner
    private readonly Channel<string> _routesToDispose = Channel.CreateUnbounded<string>();

    // Collection for tracking exceptions from routes for propagation
    private readonly ConcurrentBag<Exception> _exceptions = new();

    // Task that handles the disposal process
    private Task? _disposalTask;

    private readonly SemaphoreSlim _routeLocksLock = new(1); // Lock for managing the locks dictionary
    private readonly Dictionary<string, SemaphoreSlim> _routeLocks = new(); // Locks for route creation

    public RoutingBlock(
        string name,
        ILogger<RoutingBlock<T>> logger,
        IServiceScopeFactory scopeFactory,
        IBoundedChannelFactory channelFactory,
        RoutingBlockOptions<T> options) : base(name, options, logger)
    {
        _routingKeySelector = options.RoutingKeySelector;
        _routeResolver = options.RouteResolver;
        _routeCache = options.RouteCache;
        _scopeFactory = scopeFactory;
        _channelFactory = channelFactory;
        _routeExpiration = options.RouteExpiration;
        _logger = logger;
    }

    public void SetSource(ISourceBlock<T> source)
    {
        _source = source;
        SourceReader = _source.GetReader(this);
    }

    public ChannelReader<T> SourceReader { get; private set; }

    private void EnsureSourceReader()
    {
        if (_source is null)
        {
            throw new InvalidOperationException("No source block configured");
        }
        if (SourceReader is null)
        {
            throw new InvalidOperationException("No source reader configured");
        }
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        EnsureSourceReader();
        try
        {
            // Start the disposal task to handle route cleanup
            _disposalTask = RunDisposalProcessAsync(context.CancellationToken);

            // Run the main processing task
            await ProcessSourceAsync(context);
        }
        catch (Exception ex)
        {
            _exceptions.Add(ex);
        }
        finally
        {
            // Signal no more routes will be added for disposal
            _routesToDispose.Writer.Complete();

            // Wait for all cleanup to complete
            if (_disposalTask != null)
            {
                await _disposalTask;
            }

            // Propagate any exceptions that occurred
            if (_exceptions.Count > 0)
            {
                throw new AggregateException("Errors occurred during route execution", _exceptions);
            }
        }
    }

    private async Task ProcessSourceAsync(IDataFlowContext context)
    {
        try
        {
            await ExecuteParallelActivities(context, Options.MaxConcurrency, async (index, ctx) =>
            {
                await ExecuteStreamProcessorAsync(index, ctx);
            });

            await SourceReader.Completion; // no more items to process.
        }
        finally
        {
            // Ensure all active routes are queued for disposal
            // I guess its safe not to lock at this point.
            foreach (var routeKey in _activeRoutes.Keys)
            {
                _routesToDispose.Writer.TryWrite(routeKey);
            }
        }
    }

    protected async Task ExecuteStreamProcessorAsync(int index, IDataFlowContext context)
    {
        await foreach (var item in SourceReader.ReadAllAsync(context.CancellationToken))
        {
            var routingKey = _routingKeySelector(item);

            // Try to get an existing channel from the cache
            InputChannelBlock<T> channel;
            if (_routeCache.TryGetValue<RouteInfo<T>>(routingKey, out var routeInfo))
            {
                channel = routeInfo!.ChannelBlock;
            }
            else
            {
                // We need to initialize a new route
                channel = await GetOrCreateRouteAsync(context, routingKey, item, context.ServiceProvider, context.CancellationToken);
            }

            // Write the item to the channel
            await channel.Writer.WriteAsync(item, context.CancellationToken);
        }
    }

    private async Task RunDisposalProcessAsync(CancellationToken cancellation)
    {
        try
        {
            await foreach (var routingKey in _routesToDispose.Reader.ReadAllAsync())
            {
                try
                {
                    await DisposeRouteAsync(routingKey, cancellation);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing route {routingKey}", routingKey);
                    _exceptions.Add(ex);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in disposal process");
            _exceptions.Add(ex);
        }
    }

    private async Task DisposeRouteAsync(string routingKey, CancellationToken cancellation)
    {
        if (_activeRoutes.TryRemove(routingKey, out var route))
        {
            _logger.LogInformation("Beginning route disposal: {routingKey}", routingKey);

            // Complete the channel first
            route.Complete();

            try
            {
                // Wait for execution with timeout
                var executingTask = route.RouteExecuting.ExecutingTask;

                try
                {
                    // Use a longer timeout for stressed environments
                    await executingTask.WaitAsync(TimeSpan.FromSeconds(20), cancellation);
                    _logger.LogInformation("Route execution completed: {routingKey}", routingKey);

                    // Add a buffer delay to ensure all activities finish
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellation);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Timeout waiting for route execution: {routingKey}", routingKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in route execution: {routingKey}", routingKey);
                    _exceptions.Add(ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for route execution: {routingKey}", routingKey);
                _exceptions.Add(ex);
            }

            // Now dispose
            await route.DisposeAsync();

            _logger.LogInformation("Route disposal completed: {routingKey}", routingKey);
        }
    }

    private void OnRouteEvicted(string key, object value, EvictionReason reason)
    {
        if (value is RouteInfo<T>)
        {
            _logger.LogInformation("Route expired: {routingKey}, Reason: {reason}", key, reason);

            // Queue the route for orderly disposal
            _routesToDispose.Writer.TryWrite(key);
        }
    }

    private async Task<InputChannelBlock<T>> GetOrCreateRouteAsync(
        IDataFlowContext context,
        string routingKey,
        T item,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // First re-check cache without any locks
        if (_routeCache.TryGetValue<RouteInfo<T>>(routingKey, out var routeInfo))
        {
            return routeInfo.ChannelBlock;
        }

        // Get the lock for this route key
        var routeLock = await GetRouteLock(routingKey);
        await routeLock.WaitAsync(cancellationToken);

        try
        {
            // Check cache again under lock
            if (_routeCache.TryGetValue(routingKey, out routeInfo))
            {
                return routeInfo!.ChannelBlock;
            }

            // If an active route exists with this key, wait for it to complete before creating a new one
            if (_activeRoutes.TryGetValue(routingKey, out var existingRoute))
            {
                _logger.LogDebug("Waiting for existing route to complete: {routingKey}", routingKey);

                // Queue for disposal and wait for completion
                _routesToDispose.Writer.TryWrite(routingKey);

                try
                {
                    await existingRoute.RouteExecuting.ExecutingTask;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error waiting for existing route to complete: {routingKey}", routingKey);
                    _exceptions.Add(ex);
                }

                _logger.LogDebug("Existing route completed: {routingKey}", routingKey);
            }

            // Create a new scope for this route
            _logger.LogDebug("Creating new async scope for route: {routingKey}", routingKey);
            var routeScope = _scopeFactory.CreateAsyncScope();

            var routingContext = new RoutingContext<T>()
            {
                RoutingKey = routingKey,
                Item = item,
                ServiceProvider = routeScope.ServiceProvider
            };

            try
            {
                _logger.LogDebug("Building new flow for route: {routingKey}", routingKey);

                // Get both DataFlow and TargetBlock from resolver
                var (dataFlow, targetBlock) = _routeResolver(routingContext);

                routingContext.DataFlow = dataFlow;
                routingContext.TargetBlock = targetBlock;

                var logger = routeScope.ServiceProvider.GetRequiredService<ILogger<InputChannelBlock<T>>>();
                var channelBlock = new InputChannelBlock<T>($"{targetBlock.Name}-route", logger, _channelFactory, Options);

                routeInfo = new RouteInfo<T>(_logger, routingContext, channelBlock, routeScope);

                _logger.LogDebug("Starting DataFlow execution for route: {routingKey}", routingKey);
                routeInfo.StartFlowExecution(targetBlock, context);

                // Add to cache with expiration
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(_routeExpiration)
                    .RegisterPostEvictionCallback((key, value, reason, state) =>
                        OnRouteEvicted((string)key, value, reason));

                _routeCache.Set(routingKey, routeInfo, cacheEntryOptions);

                // Add to active routes dictionary
                _activeRoutes[routingKey] = routeInfo;

                return channelBlock;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating route: {routingKey}", routingKey);

                // Dispose resources on failure
                if (routeInfo != null)
                {
                    await routeInfo.DisposeAsync();
                }

                _exceptions.Add(ex);
                throw;
            }
        }
        finally
        {
            routeLock.Release();
        }
    }

    private async Task<SemaphoreSlim> GetRouteLock(string routeKey)
    {
        await _routeLocksLock.WaitAsync();
        try
        {
            if (!_routeLocks.TryGetValue(routeKey, out var routeLock))
            {
                routeLock = new SemaphoreSlim(1);
                _routeLocks[routeKey] = routeLock;
            }
            return routeLock;
        }
        finally
        {
            _routeLocksLock.Release();
        }
    }
}

