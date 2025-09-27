namespace Uniun.DataFlow;

using System.Diagnostics;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Metrics;

public class DataFlow<TConfig> : IDataFlow
    where TConfig : IDataFlowConfiguration
{
    private readonly IDataFlow _flow;

    public DataFlow(IDataFlow flow)
    {
        _flow = flow;
        this.Name = flow.Name ?? typeof(TConfig).Name;
        if (string.IsNullOrWhiteSpace(flow.Name))
        {
            flow.Name = typeof(TConfig).Name;
        }
    }

    public string Name { get; set; }

    public Task ExecuteAsync(IDataFlowContext context) => _flow.ExecuteAsync(context);
}

public class DataFlow : IDataFlow
{
    private readonly List<IBlock> _blocks;
    private readonly IDataFlowMetrics _metrics;

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
        context.Name ??= Name;
        Stopwatch? stopwatch = null;

        var flowMetrics = context.FlowMetricsContext = new DataFlowMetricsTagsContext(Name, context.InvocationId, _metrics);
        flowMetrics.Started();

        var isSuccessful = false;

        // --- Begin error-driven cancellation integration ---
        var userToken = context.CancellationToken;
        using var errorCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userToken, errorCts.Token);

        // Replace context's token with the linked token for this execution
        context.CancellationToken = linkedCts.Token;
        // --- End error-driven cancellation integration ---

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
                }
            }
            else
            {
                stopwatch = Stopwatch.StartNew();
            }

            try
            {

                var blockTasks = _blocks.Select(block =>
                ExecuteBlockAsync(flowActivity, block, context, errorCts));

                await Task.WhenAll(blockTasks);
                context.CancellationToken.ThrowIfCancellationRequested(); // becuse channel readers writers can gracefully exit from streams, lets ensure if we are cancelled we throw here.
                flowActivity?.SetStatus(ActivityStatusCode.Ok);
                isSuccessful = true;
            }
            catch (Exception ex)
            {
                // Signal cancellation to all blocks if any block fails
                errorCts.Cancel();

                flowActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
            finally
            {
                double flowDuration;
                if (flowActivity != null)
                {
                    flowActivity.Stop();
                    flowDuration = flowActivity.Duration.TotalMilliseconds;
                }
                else
                {
                    stopwatch?.Stop();
                    flowDuration = stopwatch?.Elapsed.TotalMilliseconds ?? 0;
                }
                flowMetrics.Completed(flowDuration, isSuccessful);
            }
        }
    }

    private async Task ExecuteBlockAsync(Activity? parentActivity, IBlock block, IDataFlowContext context, CancellationTokenSource errorCts)
    {
        var name = context.Name;
        Stopwatch? stopwatch = null;
        var isSuccessful = false;

        using var activity = ActivitySource.StartActivity(
            ActivityNames.Block,
            ActivityKind.Internal,
            parentActivity?.Context ?? default);

        if (activity is not null)
        {
            activity.AddTags(_metrics.GlobalTags);
            if (!string.IsNullOrWhiteSpace(name))
            {
                activity.AddTag(ActivityNames.TagNames.FlowName, name);
            }
            activity.AddTag(ActivityNames.TagNames.BlockName, block.Name);
            activity.DisplayName = $"{ActivityNames.Block} {{BlockName}}";
           
        }
        else
        {
            stopwatch = Stopwatch.StartNew(); //we need to resort to stopwatch for time metric as activity source is not available
        }
      
        try
        {
            await block.ExecuteAsync(context);
            activity?.SetStatus(ActivityStatusCode.Ok);
            isSuccessful = true;
        }
        catch (Exception ex)
        {
            errorCts.Cancel(); // Signal cancellation flow-wide
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            double blockDuration;
            if (activity != null)
            {
                activity.Stop();
                blockDuration = activity.Duration.TotalMilliseconds;
            }
            else
            {
                stopwatch?.Stop();
                blockDuration = stopwatch?.Elapsed.TotalMilliseconds ?? 0;
            }
            block.MetricsContext?.Completed(blockDuration, isSuccessful);
        }
    }
}
