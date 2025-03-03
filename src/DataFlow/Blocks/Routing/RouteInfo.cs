namespace Uniun.DataFlow.Blocks.Routing;
using System;
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks.InputChannel;

public class RouteInfo<T> : IAsyncDisposable
{
    private readonly AsyncServiceScope _routeScope;

    public DataFlow DataFlow { get; }

    public RouteInfo(RoutingContext<T> context, InputChannelBlock<T> channelBlock, AsyncServiceScope routeScope)
    {
        Context = context;
        ChannelBlock = channelBlock;
        _routeScope = routeScope;
        DataFlow = context.DataFlow;
    }

    public RoutingContext<T> Context { get; }
    public InputChannelBlock<T> ChannelBlock { get; }

    /// <summary>
    /// Can be used to wait for downstream consumers to complete before disposing this route.
    /// </summary>
    /// <remarks>Use to make sure the route is fully drained before disposing.</remarks>
   // public Task Completion => ChannelBlock.Reader.Completion;

    internal RouteExecution RouteExecuting { get; private set; }

    //public void Dispose()
    //{
    //    // No more writes to this route its being disposed.
    //    //ChannelBlock.Writer.TryComplete
    //    CompleteChannel();
    //    _routeScope.Dispose();
    //}

    /// <summary>
    /// Starts the downstream target block executing from the input channel for this route.
    /// </summary>
    /// <param name="block"></param>
    /// <param name="context"></param>

    /* Unmerged change from project 'DataFlow (net6.0)'
    Before:
        public void StartBlockExecution(Pipelines.DataFlow.Core.ITargetBlock<T> block, IDataFlowContext context)
        {
    After:
        public void StartBlockExecution(DataFlow.Core.ITargetBlock<T> block, IDataFlowContext context)
        {
    */
    public void StartFlowExecution(ITargetBlock<T> block, IDataFlowContext context)
    {
        block.SetSource(ChannelBlock);
        // Start executing the block and track execution
        // _logger.LogDebug("Adding executing task for route {routingKey}", routingKey);
        // Start executing the sub DataFlow for the route.
        var executingTask = DataFlow.ExecuteAsync(context);

        //var executingTask = block.ExecuteAsync(context);
        RouteExecuting = new RouteExecution(executingTask);
    }

    /// <summary>
    /// Signals completion of the route, and returns the downstream blocks completion task so you can wait for the downstream block to exit.
    /// </summary>
    /// <returns></returns>
    public Task CompleteAsync()
    {
        // signals the block to stop executing because no more data on this route.
        CompleteChannel();
        return RouteExecuting.ExecutingTask;
    }

    private void CompleteChannel()
    {
        var complete = ChannelBlock.Writer.TryComplete();
        if (!complete)
        {
            // Log warning
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            CompleteChannel();
        }
        catch (Exception ex)
        {
            // Log but continue with disposal
        }
        finally
        {
            // Dispose the scope and other resources
            await _routeScope.DisposeAsync();
        }      
       
    }

    internal class RouteExecution
    {
        public Task ExecutingTask { get; }
        public TaskCompletionSource<object> CompletionSource { get; }

        public RouteExecution(Task executingTask)
        {
            ExecutingTask = executingTask;
            CompletionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Wire up completion/error handling
            executingTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    CompletionSource.SetException(t.Exception!.InnerExceptions);
                }
                else if (t.IsCanceled)
                {
                    CompletionSource.SetCanceled();
                }
                else
                {
                    CompletionSource.SetResult(null!);
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }

}

