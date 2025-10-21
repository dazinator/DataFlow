namespace Uniun.DataFlow.Blocks;

using System;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;
using Uniun.DataFlow.Builder.Graph;
using Uniun.DataFlow.Metrics;

public abstract class BlockBase : IBlock, IDataFlowInitializable
{
    protected BlockBase(string name, BlockOptions? blockOptions, ILogger logger)
    {
        Options = blockOptions ?? new BlockOptions();
        Logger = logger;
        Name = name;
    }

    public BlockOptions Options { get; }
    public string Name { get; }

    public BlockMetricsTagsContext MetricsContext { get; set; }

    public ILogger Logger { get; }
    public FlowRateMetricsCollector FlowRateMetricsCollector { get; private set; }

    /// <summary>
    /// Records a number of operations performed by the block which helps calculate its flow rate (i.e processing speed) metric.
    /// </summary>
    /// <param name="count"></param>
    /// <remarks>This is about operations performed not data volumes. I.e a batch produced, or transform down. It should be called after the block operationally completes for an item in the stream.</remarks>
    protected void RecordOperation(int count)
    {
        // We use a rate limiter to control how often we record metrics. This method is called for potentially every block for each item in the stream where their could be millions.
        // We want to avoid producing millions of data item metrics.
        FlowRateMetricsCollector?.RecordOperation(count);
    }

    /// <summary>
    /// Records a number of operations performed by the block which helps calculate its flow rate (i.e processing speed) metric.
    /// </summary>
    /// <param name="count"></param>
    /// <remarks>This is about operations performed not data volumes. I.e a batch produced, or transform down. It should be called after the block operationally completes for an item in the stream.</remarks>
    protected void RecordOperation()
    {
        // We use a rate limiter to control how often we record metrics. This method is called for potentially every block for each item in the stream where their could be millions.
        // We want to avoid producing millions of data item metrics.
        FlowRateMetricsCollector?.RecordOperation(1);
    }
    //public DataItemMetricsContext ItemsMetricContext { get; set; }

    public async Task ExecuteAsync(IDataFlowContext context)
    {
        using var logScope = Logger.BeginScope(new Dictionary<string, object> { { "BlockName", Name } });
        Logger.LogInformation("Executing");
        SetupFlowRateCollection(context);

        //ItemsMetricContext = MetricsContext.CreateItemsContext(Options.ItemsMetricLabel);
        try
        {
            MetricsContext?.Started();
            await CoreExecuteAsync(context);
        }
        finally
        {
            if (FlowRateMetricsCollector != null)
            {
                FlowRateMetricsCollector.FlushPendingOperations();
                var readStats = FlowRateMetricsCollector.GetStatistics();
                FlowRateMetricsCollector?.Dispose();
                Logger.LogInformation("{FlowStats}", readStats);
            }
            Logger.LogInformation("Finished Executing");
        }

    }

    private void SetupFlowRateCollection(IDataFlowContext context)
    {
        // Set up flow rate metrics if enabled
        // Handle the case where FlowMetricsContext might be null (e.g., when blocks are executed directly in benchmarks)
        if (MetricsContext == null && context.FlowMetricsContext != null)
        {
            MetricsContext = context.FlowMetricsContext.CreateBlockContext(Name);
        }

        if (Options.EnableFlowRateMetrics && MetricsContext != null)
        {
            FlowRateMetricsCollector = new FlowRateMetricsCollector(MetricsContext, Options.FlowRateMetricsSamplesPerSecond);
        }
    }

    protected abstract Task CoreExecuteAsync(IDataFlowContext context);

    /// <summary>
    /// Called after all blocks in the dataflow have been instantiated during the Build phase.
    /// Override this method in derived classes to perform initialization that requires
    /// access to other blocks in the runtime graph.
    /// </summary>
    /// <param name="runtimeGraph">The runtime graph containing all instantiated blocks</param>
    /// <param name="cancellationToken">Cancellation token for the initialization process</param>
    public virtual void OnDataFlowInitialized(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken)
    {
        // Default implementation does nothing - derived classes can override
    }

    // 2. Add simple exception handling in BlockBase.ExecuteParallelActivities
    protected virtual async Task ExecuteParallelActivities(IDataFlowContext context, int howMany, ParallelActivityDelegate activity)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Options.MaxConcurrency,
            CancellationToken = context.CancellationToken
        };

        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, howMany),
                parallelOptions,
                async (index, ct) =>
                {

                    try
                    {
                        using var logScope = Logger.BeginScope(new Dictionary<string, object> { { "ParallelIndex", index } });

                        if (!Options.UseSeperateScopes)
                        {
                            Logger.LogDebug(
                                "Parallel activity starting with service provider {ServiceProvider}",
                                context.ServiceProvider.GetHashCode());

                            await activity(index, context);
                            return;
                        }

                        try
                        {
                            // Wrap scope creation in try-catch to handle disposed provider
                            await using var scope = context.CreateNewAsyncScope(out var branchContext);

                            //string actorId = Guid.NewGuid().ToString().Substring(0, 8); // Generate unique ID for this actor
                            Logger.LogDebug(
                                "Parallel activity starting with service provider {ServiceProvider}",
                                branchContext.ServiceProvider.GetHashCode());

                            await activity(index, branchContext);
                        }
                        catch (ObjectDisposedException ex)
                        {
                            // Just log and exit gracefully if provider was disposed
                            Logger.LogWarning(
                                ex,
                                "Service provider was disposed while creating scope");
                        }
                    }
                    finally
                    {
                        Logger.LogDebug(
                             "Parallel activity completed with service provider {ServiceProvider}",
                             context.ServiceProvider.GetHashCode());
                    }
                });
        }
        catch (ObjectDisposedException ex)
        {
            // Catch provider disposal at the outer level as well
            Logger.LogWarning(
                ex,
                "Service provider was disposed during parallel activities in block {blockName}",
                Name);
            throw;
        }
    }
}

public delegate Task ParallelActivityDelegate(int index, IDataFlowContext context);


