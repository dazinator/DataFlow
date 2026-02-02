namespace Uniun.DataFlow.Blocks.Routing;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Blocks.InputChannel;

/// <summary>
/// Simplified route info that doesn't handle expiration or complex disposal scenarios.
/// </summary>
/// <typeparam name="T"></typeparam>
public class PersistentRouteInfo<T> : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly AsyncServiceScope _routeScope;
    private readonly IDisposable? _logScope;
    private readonly object _completionLock = new();
    private bool _isCompleted = false;

    public RoutingContext<T> Context { get; }
    public InputChannelBlock<T> ChannelBlock { get; }
    public Task? ExecutionTask { get; private set; }

    public PersistentRouteInfo(
        ILogger logger,
        RoutingContext<T> context,
        InputChannelBlock<T> channelBlock,
        AsyncServiceScope routeScope)
    {
        _logger = logger;
        _logScope = _logger.BeginScope("Route {routingKey}", context.RoutingKey);
        Context = context;
        ChannelBlock = channelBlock;
        _routeScope = routeScope;
    }

    public void StartFlowExecution(ITargetBlock<T> targetBlock, IDataFlowContext context)
    {
        targetBlock.SetSource(ChannelBlock);

        // Create a new context with the route's service provider
        // This ensures that the route executes with its own scoped service provider
        // and prevents ObjectDisposedException when parent worker scopes are disposed
        var routeContext = new DataFlowContext(context.InvocationId)
        {
            Name = context.Name,
            ServiceProvider = Context.ServiceProvider,
            CancellationToken = context.CancellationToken,
            FlowMetricsContext = context.FlowMetricsContext,
            Items = context.Items
        };

        // Start executing both the channel block and the data flow with the route's scoped context
        var channelTask = ChannelBlock.ExecuteAsync(routeContext);
        var dataFlowTask = Context.DataFlow.ExecuteAsync(routeContext);

        // Combine both tasks
        ExecutionTask = Task.WhenAll(channelTask, dataFlowTask);

        _logger.LogDebug("Route execution started for: {routingKey}", Context.RoutingKey);
    }

    public void Complete()
    {
        lock (_completionLock)
        {
            if (_isCompleted)
            {
                return;
            }

            _isCompleted = true;

            try
            {
                ChannelBlock.Complete();
                _logger.LogDebug("Channel completed for route: {routingKey}", Context.RoutingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing channel for route: {routingKey}", Context.RoutingKey);
                throw;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _logger.LogDebug("Disposing route: {routingKey}", Context.RoutingKey);

            // Ensure completion
            Complete();

            // Wait for execution to complete
            if (ExecutionTask != null)
            {
                await ExecutionTask;
            }

            // Dispose the scope
            await _routeScope.DisposeAsync();
            _logScope?.Dispose();

            _logger.LogDebug("Route disposed: {routingKey}", Context.RoutingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing route: {routingKey}", Context.RoutingKey);
            throw;
        }
    }
}
