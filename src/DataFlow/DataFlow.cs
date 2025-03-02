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
    }

    public Task ExecuteAsync(IDataFlowContext context) => _flow.ExecuteAsync(context, typeof(TConfig).Name);
}

public class DataFlow
{
    private readonly List<IBlock> _blocks;
    private readonly IDataFlowMetrics _metrics;

    // Static fields shared across all DataFlow instances
    private static readonly ActivitySource ActivitySource = new ActivitySource("Uniun.DataFlow");

    public DataFlow(List<IBlock> blocks, IDataFlowMetrics metrics)
    {
        _blocks = blocks;
        _metrics = metrics;
    }

    public async Task ExecuteAsync(IDataFlowContext context, string name)
    {
        // Create flow-level activity
        context.AddDimension("flow.type", name)
               .AddDimension("flow.invocationid", name);

        using var flowActivity = ActivitySource.StartActivity($"DataFlow.Execute");
        flowActivity?.AddDataFlowContextDimensions(context);


        try
        {

            var blockTasks = _blocks.Select(block =>
            ExecuteBlockAsync(flowActivity, block, context, name));

            await Task.WhenAll(blockTasks);
            context.CancellationToken.ThrowIfCancellationRequested(); // becuse channel readers writers can gracefully exit from streams, lets ensure if we are cancelled we throw here.

            // Record flow-level metrics
            if (flowActivity != null)
            {
                var flowDuration = flowActivity.Duration.TotalSeconds;
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
                _metrics.FlowCompleted(flowDuration, name, context);
                    //.FlowExecutionDuration.Record(
                    //flowActivity.Duration.TotalMilliseconds,
                    //new("flow.invocationid", context.InvocationId),
                    //new("flow.type", name));

                // Add tag with total processed items
                // flowActivity.SetTag("items.processed", totalItemsProcessed);
            }
        }
        catch (Exception ex)
        {
            // Record exception in activity
            flowActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }


        //// Execute all blocks concurrently
        //var blockTasks = _blocks.Select(block =>
        //    ExecuteBlockAsync(block, context));

        //await Task.WhenAll(blockTasks);
        //context.CancellationToken.ThrowIfCancellationRequested(); // becuse channel readers writers can gracefully exit from streams, lets ensure if we are cancelled we throw here.
    }


    private async Task ExecuteBlockAsync(Activity parentActivity, IBlock block, IDataFlowContext context, string flowName)
    {
        // Create child activity for the block
        using var blockActivity = ActivitySource.CreateActivity($"Block.{block.Name}", ActivityKind.Internal);
        blockActivity?.SetParentId(parentActivity.TraceId, parentActivity.SpanId);
        blockActivity?.SetTag("block.name", block.Name);
        var dims = context.Dimensions.Select(d => new KeyValuePair<string, object>(d.Key, d.Value)).ToArray();

        try
        {
            await block.ExecuteAsync(context);
        }
        catch (Exception ex)
        {
            blockActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            // Record block duration
            if (blockActivity != null)
            {
                var blockDuration = blockActivity.Duration.TotalMilliseconds;

                
                DataFlowMetrics.BlockProcessingDuration.Record(
                    blockDuration,
                    dims);
            }
        }
    }
}
