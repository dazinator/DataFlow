namespace Uniun.DataFlow.Blocks.Routing;
using System;
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
    private readonly RoutingOptions _options;
    private ISourceBlock<T>? _source;
    private readonly ILogger<RoutingBlock<T>> _logger;

    // its ok for the cleanup channel to be unbounded, we'd have to have a large rate of route expiry for this to become a problem, and we can fix this my introducing a semaphore on the max number of active routes if we want to add backpressure later.
    private readonly Channel<RouteInfo<T>> _pendingCleanup = Channel.CreateUnbounded<RouteInfo<T>>();

    private readonly SemaphoreSlim _routeLocksLock = new(1); // Lock for managing the locks dictionary
    private readonly Dictionary<string, SemaphoreSlim> _routeLocks = new(); // when we need a lock to rotate a route, we use this dictionary to get a lock for a routing key.

    /// <summary>
    /// We need some lock on the route key, so that if there is a completion task active for that same route key (suppose the route expires, but is being drained, and meantime its initialised again - we don't want two overlapping routes for the same key)
    /// we wait for any existing one to complete before using the new route. This avoids accidentally introducing concurrency on the same route key.
    /// </summary>
    private readonly Dictionary<string, RouteInfo<T>> _allRouteCompletions = new();

    public RoutingBlock(
        string name,
        Func<T, string> routingKeySelector,
        Func<RoutingContext<T>, (DataFlow DataFlow, ITargetBlock<T> TargetBlock)> routeResolver,
        RoutingOptions options,
        ILogger<RoutingBlock<T>> logger) : base(name, options)
    {
        _routingKeySelector = routingKeySelector;
        _routeResolver = routeResolver;
        _routeCache = options.RouteCache;
        _options = options;
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
            // Run the main processing and cleanup tasks
            await Task.WhenAll(
                StartCleanupTask(context.CancellationToken),
                ProcessSourceAsync(context)
            );
        }
        finally
        {
            // We expect this to propagate all exceptions from execited routes - so it awaits their completions, and disposed of them.
            await CompleteAllActiveRoutes();
        }
    }

    private async Task CompleteAllActiveRoutes()
    {
        await _routeLocksLock.WaitAsync();
        try
        {
            // Create a list to track any exceptions
            var exceptions = new List<Exception>();

            // Try to complete each route, capturing any exceptions
            foreach (var route in _allRouteCompletions.Values)
            {
                try
                {
                    await route.CompleteAsync();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    _logger.LogError(ex, "Error completing route {RoutingKey}", route.Context.RoutingKey);
                }
            }

            // Now dispose all routes regardless of completion success
            foreach (var route in _allRouteCompletions.Values)
            {
                try
                {
                    await route.DisposeAsync();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    _logger.LogError(ex, "Error disposing route {RoutingKey}", route.Context.RoutingKey);
                }
            }

            _allRouteCompletions.Clear();

            // If we caught any exceptions, throw an aggregate exception
            if (exceptions.Count > 0)
            {
                throw new AggregateException("One or more routes failed to complete or dispose", exceptions);
            }
        }
        finally
        {
            _routeLocksLock.Release();
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
            // Signal cleanup to expect no more routes from this source it can then finish cleaning up and exit.
            _pendingCleanup.Writer.Complete();
            await CompleteAllActiveRoutes(); // flush all active routes to cleanup.

        }
    }

    protected async Task ExecuteStreamProcessorAsync(int index, IDataFlowContext context)
    {
        await foreach (var item in SourceReader.ReadAllAsync(context.CancellationToken))
        {
            var routingKey = _routingKeySelector(item);
            // First check cache without any async or locks - hot path for an active route
            InputChannelBlock<T> channel;
            if (_routeCache.TryGetValue<RouteInfo<T>>(routingKey, out var routeInfo))
            {
                channel = routeInfo!.ChannelBlock;
            }
            else
            {
                // We need to initialise a new route
                channel = await GetOrCreateRouteAsync(context, routingKey, item, context.ServiceProvider, context.CancellationToken);
            }
            await channel.Writer.WriteAsync(item, context.CancellationToken);
        }
    }

    private async Task StartCleanupTask(CancellationToken cancellation)
    {
        const int cleanupDelay = 2;  // seconds
        await foreach (var expiredRoute in _pendingCleanup!.Reader.ReadAllAsync()) // we dont use a cancellation token because we always drain the cleanup channel fully before exiting.
        {
            // Add delay after dequeue as small safety net in case some weird issue where the route may have just been removed from the cache and sent to us for cleanup, but is actually still in use with a call to WriteAsync in progress.. we want to give it a chance to complete.
            // this should not happen and could be over-cautions, but lets do it anyway.
            await Task.Delay(TimeSpan.FromSeconds(cleanupDelay), cancellation);

            // We don't need to await the completion of the route here, we just need to dispose it so that any downstream consumers can complete.
            // The main ExecuteAsync method will await all routes Completions before exiting and this will ensure exceptions are propagated.
            await expiredRoute.DisposeAsync(); // allows downstream blocks to finsish by signalling completion.
            _logger.LogInformation("Route cleanup completed: {routing-key}", expiredRoute.Context.RoutingKey);
        }

    }

    private bool _cannotWriteEvictedRouteToCleanup = false;

    private void OnRouteEvicted(string key, object value, EvictionReason reason)
    {
        if (value is RouteInfo<T> evictedRoute)
        {
            _logger.LogInformation(
                "Route expired: {routingKey}, Reason: {reason}",
                key, reason);

            // This is the only but of nastiness in this block - we need to write the evicted route to the cleanup channel but this callback is non async.
            // TryWrite() should never really not succeed to an unbounded channel, but we play it extra safe here.
            if (!_pendingCleanup.Writer.TryWrite(evictedRoute))
            {
                var attempt = 1;
                while (!_pendingCleanup.Writer.TryWrite(evictedRoute))
                {
                    attempt++;
                    if (attempt > 5)
                    {
                        // We don't surface an exception here because we are in a callback and it might be swallowed or unobserved.
                        // so we capture this rare scenario with a flag. Then in our main async processing loop we can surface the problem by throwing an exception there, should we ever be in this odd state.
                        _cannotWriteEvictedRouteToCleanup = true;
                    }
                }
            }

        }
    }

    private async Task<InputChannelBlock<T>> GetOrCreateRouteAsync(
        IDataFlowContext context,
         string routingKey,
         T item,
         IServiceProvider serviceProvider,
         CancellationToken cancellationToken)
    {
        // First re check cache without any locks
        if (_routeCache.TryGetValue<RouteInfo<T>>(routingKey, out var routeInfo))
        {
            return routeInfo.ChannelBlock;
        }

        // We need to initialise a new route
        // Get the lock for this route key.
        var routeLock = await GetRouteLock(routingKey);
        await routeLock.WaitAsync(cancellationToken); // two may try but only one will pass for this routing key.
        try
        {
            // Check cache again under lock because our predecessor may have just added the route.
            if (_routeCache.TryGetValue(routingKey, out routeInfo))
            {
                return routeInfo!.ChannelBlock;
            }

            // This is an edge case where we are in a state where we are failing to write expired blocks to the cleanup channel.
            // This could introduce a problem if we allow new routes to be created without old expired ones being cleaned up properly, so here is a good place to surface
            // our exception.
            if (_cannotWriteEvictedRouteToCleanup)
            {
                throw new InvalidOperationException($"Failed to write an expired route to cleanup channel after 10 attempts.");
            }

            // We want to avoid initialising a new route and executing a new block for the same routing key
            // if there is possible an expired block that is still executing
            // Otherwise we might accidentally introduce concurrency on the same route key by putting this new route into usage whilst the old expired is still running.

            if (_allRouteCompletions.TryGetValue(routingKey, out var route))
            {
                // Wait for any active subflow for this stale key to complete so we don't cause accidental parallelism.
                // Need to think about what happens if the sub flow errors, how do we propagate these errors back to the main flow?
                //try
                //{
                await route.RouteExecuting.ExecutingTask;

                //}
                //catch (Exception e)
                //{
                // we could add them to a bag, and then throw an aggregate exception at the end from this block execute method?
                //    _logger.LogError(e, "Error in stale execution for routing key: {routingKey}", routingKey);
                // throw;
                // }

                // also we've awaited it in line with the execution flow so we don't need to worry about propogating exceptions from it at the end of execution.
                _allRouteCompletions.Remove(routingKey, out _);

            }

            // create a new scope for this route.
            var routeScope = serviceProvider.CreateAsyncScope();
            var routingContext = new RoutingContext<T>()
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

                var channelBlock = new InputChannelBlock<T>($"{targetBlock.Name}-route", Options);
                routeInfo = new RouteInfo<T>(routingContext, channelBlock, routeScope);

                _logger.LogDebug("Starting DataFlow execution for route: {routingKey}", routingKey);
                routeInfo.StartBlockExecution(targetBlock, context);

                var cacheEntryOptions = new MemoryCacheEntryOptions()
              .SetSlidingExpiration(_routeExpiration)
              .RegisterPostEvictionCallback((key, value, reason, state) =>
                  OnRouteEvicted((string)key, value, reason));
                _routeCache.Set(routingKey, routeInfo, cacheEntryOptions);

                _allRouteCompletions.TryAdd(routingKey, routeInfo);

                return channelBlock;
            }
            catch
            {
                // Any problem with the new route setup we need to dispose it so any downstream consumers can complete immediately.
                if (routeInfo is not null)
                {
                    await routeInfo.DisposeAsync();
                }
                throw; // this is still a surfacable exception.
            }
        }
        finally
        {
            // we are done with this routing key, release the lock.
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

