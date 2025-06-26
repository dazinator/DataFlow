namespace Uniun.DataFlow;

using System.Diagnostics;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Metrics;

// Generic wrapper that uses the config type as the type parameter
public class DataFlow<TConfig> : IDataFlow
    where TConfig : IDataFlowConfiguration
{
    private readonly DataFlow _flow;

    public DataFlow(DataFlow flow)
    {
        _flow = flow;
        if (string.IsNullOrWhiteSpace(flow.Name))
        {
            flow.Name = typeof(TConfig).Name;
        }
    }

    public Task ExecuteAsync(IDataFlowContext context) => _flow.ExecuteAsync(context);
}

public class DataFlow
{
    private readonly List<IBlock> _blocks;
    private readonly IDataFlowMetrics _metrics;

    // Static fields shared across all DataFlow instances
    private static readonly ActivitySource ActivitySource = new ActivitySource("Uniun.DataFlow");

    public DataFlow(
        string name,
        List<IBlock> blocks,
        IDataFlowMetrics metrics)
    {
        _blocks = blocks;
        _metrics = metrics;
        Name = name;
    }

    public string Name { get; set; }

    public async Task ExecuteAsync(IDataFlowContext context)
    {
        // Create flow-level activity
        context.Name ??= Name;
        Stopwatch? stopwatch = null;


        var isSuccessful = false; // Add this

        using (var flowActivity = ActivitySource.StartActivity(ActivityNames.Flow))
        {
            if (flowActivity is not null)
            {
                flowActivity.AddTags(_metrics.GlobalTags);
                flowActivity.AddTag(ActivityNames.TagNames.FlowInvocationId, context.InvocationId);
                if (!string.IsNullOrWhiteSpace(context.Name))
                {
                    flowActivity.AddTag(ActivityNames.TagNames.FlowName, context.Name);
                    flowActivity.DisplayName = $"{ActivityNames.Flow} {{FlowName}}";
                    //flowActivity.OperationName
                }
            }
            else
            {
                stopwatch = Stopwatch.StartNew(); //we need to resort to stopwatch for time metric as activity source is not available
            }

            try
            {

                var blockTasks = _blocks.Select(block =>
                ExecuteBlockAsync(flowActivity, block, context));

                await Task.WhenAll(blockTasks);
                context.CancellationToken.ThrowIfCancellationRequested(); // becuse channel readers writers can gracefully exit from streams, lets ensure if we are cancelled we throw here.
                flowActivity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                // Record exception in activity
                flowActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
            finally
            {
                // Record flow-level metrics
                if (flowActivity != null)
                {
                    // flowActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    flowActivity.Stop();
                    var flowDuration = flowActivity.Duration.TotalMilliseconds;
                    //if (flowDuration > 0)
                    //{
                    //    // Calculate throughput (items/second)
                    //    double throughput = totalItemsProcessed / flowDuration;
                    //    DataFlowMetrics.FlowThroughput.Record(
                    //        throughput,
                    //        new("flow.id", _flowInstanceId),
                    //        new("flow.type", typeof(T).Name));
                    //}

                    // Record total flow duration
                    _metrics.FlowCompleted(flowDuration, context.Name, context, flowActivity.Status == ActivityStatusCode.Ok);
                    //.FlowExecutionDuration.Record(
                    //flowActivity.Duration.TotalMilliseconds,
                    //new("flow.invocationid", context.InvocationId),
                    //new("flow.type", name));

                    // Add tag with total processed items
                    // flowActivity.SetTag("items.processed", totalItemsProcessed);
                }
                else
                {
                    stopwatch?.Stop(); // Add this
                    var flowDuration = stopwatch?.Elapsed.TotalMilliseconds ?? 0;
                    _metrics.FlowCompleted(flowDuration, context.Name, context, isSuccessful);
                }
            }


        }






        //// Execute all blocks concurrently
        //var blockTasks = _blocks.Select(block =>
        //    ExecuteBlockAsync(block, context));

        //await Task.WhenAll(blockTasks);
        //context.CancellationToken.ThrowIfCancellationRequested(); // becuse channel readers writers can gracefully exit from streams, lets ensure if we are cancelled we throw here.
    }


    private async Task ExecuteBlockAsync(Activity? parentActivity, IBlock block, IDataFlowContext context)
    {
        var name = context.Name;

        // Create the activity within the current activity's context
        using var activity = ActivitySource.StartActivity(
            ActivityNames.Block,
            ActivityKind.Internal,
            parentActivity?.Context ?? default); // Use ActivityContext instead of manual ID setting


        if (activity is not null)
        {
            activity.AddTags(_metrics.GlobalTags);
            if (!string.IsNullOrWhiteSpace(name))
            {
                activity.AddTag(ActivityNames.TagNames.FlowName, name);
            }
            activity.AddTag(ActivityNames.TagNames.BlockName, block.Name);
            activity.DisplayName = $"{ActivityNames.Block} {{BlockName}}";

            // Add extra contextual information
            // activity.AddTag("block.type", block.GetType().Name);
        }

        // blockActivity?.AddTag(DataFlowMetrics.TagNames.FlowInvocationId, context.InvocationId);
        try
        {
            await block.ExecuteAsync(context);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            // Record block duration
            if (activity != null)
            {
                // Make sure all tags are set before stopping
                activity.Stop();
                var blockDuration = activity.Duration.TotalMilliseconds;
                _metrics.BlockCompleted(blockDuration, name, block.Name, context, activity.Status == ActivityStatusCode.Ok);
            }
        }


    }
}
