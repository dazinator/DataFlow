namespace Uniun.DataFlow.Blocks.Routing;
using System;
using global::Uniun.DataFlow.Blocks.InputChannel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class RouteInfo<T> : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly AsyncServiceScope _routeScope;
    private readonly IDisposable? _logScope;

    // Lock for coordinating completion and disposal
    private readonly object _lifecycleLock = new object();
    private bool _isCompleted = false;
    private bool _isDisposed = false;

    public IDataFlow DataFlow { get; }
    public RoutingContext<T> Context { get; }
    public InputChannelBlock<T> ChannelBlock { get; }
    internal RouteExecution? RouteExecuting { get; private set; }

    public RouteInfo(ILogger logger, RoutingContext<T> context, InputChannelBlock<T> channelBlock, AsyncServiceScope routeScope)
    {
        _logger = logger;
        _logScope = _logger.BeginScope("Route {routingKey}", context.RoutingKey);
        Context = context;
        ChannelBlock = channelBlock;
        _routeScope = routeScope;
        DataFlow = context.DataFlow;
    }

    /// <summary>
    /// Starts the downstream target block executing from the input channel for this route.
    /// </summary>
    public void StartFlowExecution(ITargetBlock<T> block, IDataFlowContext context)
    {
        block.SetSource(ChannelBlock);

        // Start executing the sub DataFlow for the route
        var channelBlock = ChannelBlock.ExecuteAsync(context);
        var executingTask = DataFlow.ExecuteAsync(context);
        var allTasks = Task.WhenAll(channelBlock, executingTask);
        RouteExecuting = new RouteExecution(_logger, allTasks);
    }

    /// <summary>
    /// Signals completion of the route by completing the channel writer.
    /// This stops the route from accepting new items.
    /// </summary>
    public void Complete()
    {
        lock (_lifecycleLock)
        {
            if (_isCompleted)
            {
                return;
            }

            _isCompleted = true;
            CompleteChannel();
        }
    }

    /// <summary>
    /// Completes the channel to signal no more items will be accepted.
    /// </summary>
    private void CompleteChannel()
    {
        try
        {
            ChannelBlock.Complete();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing channel for route {routingKey}", Context.RoutingKey);
        }
    }

    public async ValueTask DisposeAsync()
    {
        bool shouldDispose;
        lock (_lifecycleLock)
        {
            shouldDispose = !_isDisposed;
            _isDisposed = true;
        }

        if (!shouldDispose)
        {
            // Log that we're starting disposal
            _logger.LogWarning("DisposeAsync called on already disposed route {routingKey}", Context.RoutingKey);
            return;
        }

        try
        {
            // Ensure channel is completed
            Complete();

            // Log that we're starting disposal
            _logger.LogDebug("Starting disposal of route {routingKey}", Context.RoutingKey);

            // CRITICAL: Wait for flow execution to fully complete
            // Including a reasonable buffer time after completion
            if (RouteExecuting != null)
            {
                _logger.LogDebug("Waiting for route execution to complete: {routingKey}", Context.RoutingKey);
                try
                {
                    // Wait for execution to complete with a reasonable timeout
                    await RouteExecuting.ExecutingTask.WaitAsync(TimeSpan.FromSeconds(20));

                    // After execution completes, add a buffer delay to ensure all operations finish
                    //  _logger.LogDebug("Route execution completed, waiting buffer period: {routingKey}", Context.RoutingKey);
                    // await Task.Delay(TimeSpan.FromSeconds(2));
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Timeout waiting for route execution: {routingKey}", Context.RoutingKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error waiting for route execution: {routingKey}", Context.RoutingKey);
                }
            }

            _logger.LogDebug("Disposing service scope for route {routingKey}", Context.RoutingKey);
            await _routeScope.DisposeAsync();
            _logScope?.Dispose();

            _logger.LogDebug("Route resources disposed for {routingKey}", Context.RoutingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during route disposal: {routingKey}", Context.RoutingKey);
            throw;
        }
    }

    /// <summary>
    /// Class to track and manage the execution of a route's flow.
    /// </summary>
    internal class RouteExecution
    {
        private readonly ILogger _logger;

        public Task ExecutingTask { get; }
        public TaskCompletionSource<object> CompletionSource { get; }

        public RouteExecution(ILogger logger, Task executingTask)
        {
            _logger = logger;
            ExecutingTask = executingTask;
            CompletionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Wire up completion/error handling
            executingTask.ContinueWith(t =>
            {
                _logger.LogDebug("Route flow execution finished, propagating completion result");

                if (t.IsFaulted)
                {
                    _logger.LogDebug(t.Exception, "Propagating exception");
                    CompletionSource.SetException(t.Exception!.InnerExceptions);
                }
                else if (t.IsCanceled)
                {
                    _logger.LogDebug("Propagating cancellation");
                    CompletionSource.SetCanceled();
                }
                else
                {
                    _logger.LogDebug("Propagating successful completion");
                    CompletionSource.SetResult(null!);
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
